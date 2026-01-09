using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StarApi.Data;
using StarApi.DTOs.User;
using StarApi.Models;
using StarApi.Services.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using BCrypt.Net;

namespace StarApi.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly IFileStorageService _fileStorageService;
        private readonly IEmailService _emailService;

        public UserService(
            AppDbContext context,
            ILogger<UserService> logger,
            IFileStorageService fileStorageService,
            IEmailService emailService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        public async Task<ProfileUpdateResultDto?> UpdateProfileWithAvatarAsync(
            Guid userId,
            UpdateProfileDto profileDto,
            IFormFile? avatarFile)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null || !await IsUserActiveAsync(userId))
                {
                    return null;
                }

                bool hasChanges = false;
                bool requiresEmailVerification = false;
                string? newAvatarUrl = null;

                // 1. Update username
                if (!string.IsNullOrWhiteSpace(profileDto.Username) &&
                    profileDto.Username != user.Username)
                {
                    var usernameExists = await _context.Users
                        .AnyAsync(u => u.Username == profileDto.Username && u.Id != userId);

                    if (usernameExists)
                    {
                        throw new InvalidOperationException("Username is already taken");
                    }

                    user.Username = profileDto.Username.Trim();
                    hasChanges = true;
                }

                // 2. Update email
                if (!string.IsNullOrWhiteSpace(profileDto.Email) &&
                    profileDto.Email != user.Email)
                {
                    // Validate email
                    try { new MailAddress(profileDto.Email); }
                    catch { throw new ArgumentException("Invalid email format"); }

                    var emailExists = await _context.Users
                        .AnyAsync(u => u.Email == profileDto.Email && u.Id != userId);

                    if (emailExists)
                    {
                        throw new InvalidOperationException("Email is already in use");
                    }

                    user.Email = profileDto.Email.Trim();
                    user.IsVerified = false;
                    user.VerificationToken = Guid.NewGuid().ToString("N");
                    user.VerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
                    hasChanges = true;
                    requiresEmailVerification = true;

                    // Send verification email
                    await SendVerificationEmailAsync(user);
                }

                // 3. Update phone
                if (profileDto.Phone != null && profileDto.Phone != user.Phone)
                {
                    user.Phone = profileDto.Phone.Trim();
                    hasChanges = true;
                }

                if (avatarFile != null)
                {
                    if (!_fileStorageService.IsValidImage(avatarFile))
                    {
                        throw new ArgumentException("Invalid image file (max 5MB, JPG/PNG/GIF/WebP)");
                    }

                    // Method 1: Use SaveAvatarToUserAsync (direct to User table)
                    if (_fileStorageService is LocalFileStorageService localService)
                    {
                        newAvatarUrl = await localService.SaveAvatarToUserAsync(userId, avatarFile);
                    }
                    // Method 2: Use SaveImageAsync (generic, returns placeholder)
                    else
                    {
                        var fileName = $"avatar_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(avatarFile.FileName)}";
                        var fileReference = await _fileStorageService.SaveImageAsync(avatarFile, "avatars", fileName);
                        newAvatarUrl = await _fileStorageService.GetFileUrlAsync(fileReference);
                    }

                    user.AvatarUrl = newAvatarUrl;
                    user.AvatarUpdatedAt = DateTime.UtcNow;
                    hasChanges = true;
                }

                // Save changes
                if (hasChanges)
                {
                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                else
                {
                    await transaction.RollbackAsync();
                }

                return new ProfileUpdateResultDto
                {
                    Profile = MapToUserProfileDto(user),
                    AvatarUrl = newAvatarUrl ?? user.AvatarUrl,
                    RequiresEmailVerification = requiresEmailVerification
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<string> GetAvatarUrlAsync(Guid userId, IFormFile avatarFile)
        {
            // Check if the service has SaveAvatarToUserAsync method (SQLite storage)
            var serviceType = _fileStorageService.GetType();
            var saveAvatarMethod = serviceType.GetMethod("SaveAvatarToUserAsync");

            if (saveAvatarMethod != null)
            {
                // Use SQLite storage
                try
                {
                    var result = saveAvatarMethod.Invoke(_fileStorageService, new object[] { userId, avatarFile });
                    if (result is Task<string> task)
                    {
                        return await task;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving avatar to SQLite");
                    throw;
                }
            }

            // Fallback to file system
            var fileName = $"avatar_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(avatarFile.FileName)}";
            var filePath = await _fileStorageService.SaveImageAsync(avatarFile, "avatars", fileName);
            return await _fileStorageService.GetFileUrlAsync(filePath);
        }

        public async Task<string?> UploadAvatarAsync(Guid userId, IFormFile imageFile)
        {
            try
            {
                if (!_fileStorageService.IsValidImage(imageFile))
                {
                    throw new ArgumentException("Invalid image file");
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return null;

                // Get avatar URL
                string? avatarUrl = await GetAvatarUrlAsync(userId, imageFile);

                // Clear old avatar data
                user.AvatarData = null;
                user.AvatarMimeType = null;
                user.AvatarUpdatedAt = null;

                // Update user
                user.AvatarUrl = avatarUrl ?? string.Empty;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return avatarUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> DeleteAvatarAsync(Guid userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found", userId);
                    return false;
                }

                // Check if user has an avatar (either in blob or URL)
                bool hasAvatar = !string.IsNullOrEmpty(user.AvatarUrl) || user.AvatarData != null;

                if (!hasAvatar)
                {
                    _logger.LogInformation("User {UserId} already has no avatar", userId);
                    return true; // Nothing to delete, return true
                }

                _logger.LogInformation("Deleting avatar for user {UserId}. URL: {AvatarUrl}, HasBlobData: {HasData}",
                    userId, user.AvatarUrl, user.AvatarData != null);

                // If using file storage service (for URL-based avatars)
                bool deleteSuccess = true;
                if (!string.IsNullOrEmpty(user.AvatarUrl))
                {
                    deleteSuccess = await _fileStorageService.DeleteImageAsync(user.AvatarUrl);
                    if (!deleteSuccess)
                    {
                        _logger.LogWarning("Failed to delete avatar file: {AvatarUrl}", user.AvatarUrl);
                        // Continue anyway to clear DB fields
                    }
                }

                // Clear all avatar-related fields from database
                user.AvatarData = null;
                user.AvatarMimeType = null;
                user.AvatarUpdatedAt = null;
                user.AvatarUrl = string.Empty; // Clear the URL too
                user.UpdatedAt = DateTime.UtcNow;

                int saved = await _context.SaveChangesAsync();

                _logger.LogInformation("Avatar cleared from database for user {UserId}. Saved: {Saved}", userId, saved);

                return saved > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting avatar for user {UserId}", userId);
                return false;
            }
        }

        public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user == null ? null : MapToUserProfileDto(user);
        }

        public async Task<UserProfileDto?> UpdateUserProfileAsync(Guid userId, UpdateProfileDto updateDto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !await IsUserActiveAsync(userId))
                return null;

            bool hasChanges = false;

            if (!string.IsNullOrWhiteSpace(updateDto.Username) && updateDto.Username != user.Username)
            {
                var usernameExists = await _context.Users
                    .AnyAsync(u => u.Username == updateDto.Username && u.Id != userId);

                if (usernameExists)
                    throw new InvalidOperationException("Username is already taken");

                user.Username = updateDto.Username.Trim();
                hasChanges = true;
            }

            if (updateDto.Phone != null && updateDto.Phone != user.Phone)
            {
                user.Phone = updateDto.Phone.Trim();
                hasChanges = true;
            }

            if (hasChanges)
            {
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return MapToUserProfileDto(user);
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
        {
            // Validate input
            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.CurrentPassword) ||
                string.IsNullOrWhiteSpace(dto.NewPassword) ||
                string.IsNullOrWhiteSpace(dto.ConfirmPassword))
            {
                return false;
            }

            // Check if new passwords match
            if (dto.NewPassword != dto.ConfirmPassword)
            {
                return false;
            }

            // Validate password strength (optional but recommended)
            if (dto.NewPassword.Length < 8)
            {
                return false;
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Verify current password
            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                return false;
            }

            // Check if new password is different from current password
            if (BCrypt.Net.BCrypt.Verify(dto.NewPassword, user.PasswordHash))
            {
                return false;
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            // Save changes and verify it was successful
            var rowsAffected = await _context.SaveChangesAsync();
            return rowsAffected > 0;
        }

        public async Task<bool> IsUserActiveAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user != null &&
                   user.Status == "Active";
        }

        public async Task<(string email, bool isVerified)> GetEmailVerificationStatusAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user == null ? (string.Empty, false) : (user.Email ?? string.Empty, user.IsVerified);
        }

        public async Task<bool> ResendVerificationEmailAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.IsVerified) return false;

            user.VerificationToken = Guid.NewGuid().ToString("N");
            user.VerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public Task<UserActivityDto?> GetUserActivityAsync(Guid userId)
        {
            return Task.FromResult<UserActivityDto?>(null);
        }

        #region Private Methods

        private async Task SendVerificationEmailAsync(User user)
        {
            try
            {
                await _emailService.SendVerificationEmailAsync(user.Email, user.VerificationToken!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email");
            }
        }

        private UserProfileDto MapToUserProfileDto(User user)
        {
            return new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                Phone = user.Phone,
                Role = user.Role,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                LastLoginAt = user.LastLoginAt,
                IsVerified = user.IsVerified,
            };
        }

        #endregion

        // Admin operations
        public async Task<(IEnumerable<UserDto> users, int total)> GetUsersAsync(UserQueryParamsDto query)
        {
            var q = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var status = (query.Status ?? string.Empty).Trim().ToLower();
                q = q.Where(u => (u.Status ?? string.Empty).ToLower() == status);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim().ToLower();
                q = q.Where(u => (u.Username ?? string.Empty).ToLower().Contains(term)
                              || (u.Email ?? string.Empty).ToLower().Contains(term)
                              || (u.Phone ?? string.Empty).ToLower().Contains(term));
            }

            var sortOrder = (query.SortOrder ?? "asc").ToLower() == "desc" ? "desc" : "asc";
            var sortBy = (query.SortBy ?? "createdAt").ToLower();

            q = sortBy switch
            {
                "id" => sortOrder == "asc" ? q.OrderBy(u => u.Id) : q.OrderByDescending(u => u.Id),
                "username" => sortOrder == "asc" ? q.OrderBy(u => u.Username) : q.OrderByDescending(u => u.Username),
                "email" => sortOrder == "asc" ? q.OrderBy(u => u.Email) : q.OrderByDescending(u => u.Email),
                "phone" => sortOrder == "asc" ? q.OrderBy(u => u.Phone) : q.OrderByDescending(u => u.Phone),
                "role" => sortOrder == "asc" ? q.OrderBy(u => u.Role) : q.OrderByDescending(u => u.Role),
                "status" => sortOrder == "asc" ? q.OrderBy(u => u.Status) : q.OrderByDescending(u => u.Status),
                "updatedat" => sortOrder == "asc" ? q.OrderBy(u => u.UpdatedAt) : q.OrderByDescending(u => u.UpdatedAt),
                "createdat" => sortOrder == "asc" ? q.OrderBy(u => u.CreatedAt) : q.OrderByDescending(u => u.CreatedAt),
                _ => sortOrder == "asc" ? q.OrderBy(u => u.CreatedAt) : q.OrderByDescending(u => u.CreatedAt)
            };

            var total = await q.CountAsync();
            var page = query.Page <= 0 ? 1 : query.Page;
            var pageSize = query.PageSize <= 0 ? 10 : query.PageSize;
            var skip = (page - 1) * pageSize;

            var users = await q.Skip(skip).Take(pageSize)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    AvatarUrl = u.AvatarUrl,
                    Phone = u.Phone,
                    Role = NormalizeRoleForFrontend(u.Role),
                    Status = NormalizeStatusForFrontend(u.Status),
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt,
                    IsVerified = u.IsVerified,
                    LastLoginAt = u.LastLoginAt
                })
                .ToListAsync();

            return (users, total);
        }

        public async Task<UserDto?> GetUserByIdAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return null;
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                Phone = user.Phone,
                Role = NormalizeRoleForFrontend(user.Role),
                Status = NormalizeStatusForFrontend(user.Status),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsVerified = user.IsVerified,
                LastLoginAt = user.LastLoginAt
            };
        }



        public async Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserAdminDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return null;

            // Normalize empty strings to null for optional fields
            var username = string.IsNullOrWhiteSpace(dto.Username) ? null : dto.Username.Trim();
            var email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            var phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            var role = string.IsNullOrWhiteSpace(dto.Role) ? null : dto.Role.Trim();
            var status = string.IsNullOrWhiteSpace(dto.Status) ? null : dto.Status.Trim();

            if (username != null && username != user.Username)
            {
                var exists = await _context.Users.AnyAsync(u => u.Username == username && u.Id != id);
                if (exists) throw new InvalidOperationException("Username is already taken");
                user.Username = username;
            }

            if (email != null && email != user.Email)
            {
                // Validate email format
                try { new System.Net.Mail.MailAddress(email); }
                catch { throw new ArgumentException("Invalid email format"); }

                var exists = await _context.Users.AnyAsync(u => u.Email == email && u.Id != id);
                if (exists) throw new InvalidOperationException("Email is already in use");
                user.Email = email;
            }

            if (phone != null && phone != user.Phone)
            {
                user.Phone = phone;
            }

            if (role != null)
            {
                user.Role = NormalizeRole(role);
            }

            if (status != null)
            {
                user.Status = NormalizeStatus(status);
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await GetUserByIdAsync(user.Id);
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;
            _context.Users.Remove(user);
            var saved = await _context.SaveChangesAsync();
            return saved > 0;
        }

        private static string NormalizeRole(string role)
        {
            var r = (role ?? "user").Trim().ToLower();
            return r switch
            {
                "admin" => "Admin",
                "editor" => "Editor",
                _ => "User"
            };
        }

        private static string NormalizeStatus(string status)
        {
            var s = (status ?? "active").Trim().ToLower();
            return s switch
            {
                "inactive" => "Inactive",
                "pending" => "Pending",
                _ => "Active"
            };
        }

        private static string NormalizeRoleForFrontend(string role)
        {
            var r = (role ?? "User").Trim().ToLower();
            return r;
        }

        private static string NormalizeStatusForFrontend(string status)
        {
            var s = (status ?? "Active").Trim().ToLower();
            return s;
        }
    }
}
