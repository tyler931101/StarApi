using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using StarApi.Data;
using StarApi.Models;
using StarApi.Services.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StarApi.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly ILogger<LocalFileStorageService> _logger;
        private readonly FileStorageSettings _settings;
        private readonly AppDbContext _context;

        public LocalFileStorageService(
            ILogger<LocalFileStorageService> logger,
            IOptions<FileStorageSettings> settings,
            AppDbContext context)
        {
            _logger = logger;
            _settings = settings.Value;
            _context = context;
        }

        public Task<string> SaveImageAsync(IFormFile imageFile, string folderName, string? fileName = null)
        {
            try
            {
                // Validate image
                if (!IsValidImage(imageFile))
                {
                    throw new ArgumentException("Invalid image file");
                }

                // Generate unique filename if not provided
                fileName ??= $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";

                // For SQLite, we return a placeholder identifier
                // Actual saving happens in UpdateUserAvatarAsync
                return Task.FromResult($"sqlite:{Guid.NewGuid()}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image");
                throw;
            }
        }

        public async Task<bool> DeleteImageAsync(string identifier)
        {
            try
            {
                // If identifier is "sqlite:userId" format
                if (IsSqliteAvatarReference(identifier, out var userId))
                {
                    return await ClearUserAvatarAsync(userId);
                }

                // For SQLite blob storage, we don't delete files, just clear the blob
                // So if identifier is a user ID or email, clear that user's avatar
                if (Guid.TryParse(identifier, out var userGuid))
                {
                    return await ClearUserAvatarAsync(userGuid);
                }

                // If it's a filename that doesn't exist as a physical file
                // (because it's in SQLite), return true
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteImageAsync for identifier: {Identifier}", identifier);
                return false;
            }
        }

        private bool IsSqliteAvatarReference(string identifier, out Guid userId)
        {
            userId = Guid.Empty;

            if (identifier.StartsWith("sqlite:") && identifier.Length > 7)
            {
                var idPart = identifier[7..]; // Remove "sqlite:" prefix
                return Guid.TryParse(idPart, out userId);
            }

            return Guid.TryParse(identifier, out userId);
        }

        private async Task<bool> ClearUserAvatarAsync(Guid userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                user.AvatarData = null;
                user.AvatarMimeType = null;
                user.AvatarUrl = string.Empty;
                user.UpdatedAt = DateTime.UtcNow;

                return await _context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing avatar for user {UserId}", userId);
                return false;
            }
        }

        public Task<string> GetFileUrlAsync(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return Task.FromResult(string.Empty);

            // Check if it's a SQLite avatar reference
            if (IsSqliteAvatarReference(identifier, out var userId))
            {
                return Task.FromResult($"{_settings.BaseUrl}/api/users/{userId}/avatar");
            }

            // Fallback for existing file system paths
            return Task.FromResult($"{_settings.BaseUrl}/uploads/{identifier}");
        }

        public bool IsValidImage(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return false;

            // Check file size
            if (imageFile.Length > _settings.MaxFileSize)
                return false;

            // Check file extension
            var allowedExtensions = _settings.AllowedExtensions;
            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return false;

            // Check MIME type
            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedMimeTypes.Contains(imageFile.ContentType.ToLowerInvariant()))
                return false;

            return true;
        }

        public (int width, int height) GetImageDimensions(IFormFile imageFile)
        {
            try
            {
                using var image = Image.Load(imageFile.OpenReadStream());
                return (image.Width, image.Height);
            }
            catch
            {
                return (0, 0);
            }
        }

        public async Task<string> OptimizeImageAsync(IFormFile imageFile, int maxWidth = 800)
        {
            try
            {
                using var image = Image.Load(imageFile.OpenReadStream());

                // Resize if width exceeds maxWidth
                if (image.Width > maxWidth)
                {
                    var ratio = (double)maxWidth / image.Width;
                    var newHeight = (int)(image.Height * ratio);

                    image.Mutate(x => x.Resize(maxWidth, newHeight));
                }

                // Save optimized image to memory stream
                using var memoryStream = new MemoryStream();
                await image.SaveAsWebpAsync(memoryStream); // Save as WebP for better compression

                return Convert.ToBase64String(memoryStream.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing image");
                throw;
            }
        }

        public async Task<bool> UserHasAvatarAsync(Guid userId)
        {
            var hasAvatar = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.AvatarData != null && u.AvatarData.Length > 0)
                .FirstOrDefaultAsync();

            return hasAvatar;
        }

        public async Task<string> SaveAvatarToUserAsync(Guid userId, IFormFile avatarFile)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    throw new ArgumentException("User not found");

                // Optimize the image
                var optimizedBytes = await OptimizeImageToBytesAsync(avatarFile, 800);
                var mimeType = "image/webp"; // Always use WebP for optimal storage

                // Store in user record
                user.AvatarData = optimizedBytes;
                user.AvatarMimeType = mimeType;
                user.AvatarUpdatedAt = DateTime.UtcNow;
                user.AvatarUrl = $"/api/users/{userId}/avatar"; // Store URL path
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Return the URL for frontend
                return $"{_settings.BaseUrl}{user.AvatarUrl}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving avatar to SQLite for user {UserId}", userId);
                throw;
            }
        }

        // NEW: Get avatar from user record
        public async Task<(byte[] data, string mimeType)> GetUserAvatarAsync(Guid userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.AvatarData, u.AvatarMimeType })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (user == null || user.AvatarData == null || user.AvatarData.Length == 0)
                throw new FileNotFoundException("Avatar not found");

            return (user.AvatarData, user.AvatarMimeType ?? "image/webp");
        }

        private async Task<byte[]> OptimizeImageToBytesAsync(IFormFile imageFile, int maxWidth = 800)
        {
            using var image = Image.Load(imageFile.OpenReadStream());

            // Resize if needed
            if (image.Width > maxWidth)
            {
                var ratio = (double)maxWidth / image.Width;
                var newHeight = (int)(image.Height * ratio);
                image.Mutate(x => x.Resize(maxWidth, newHeight));
            }

            using var memoryStream = new MemoryStream();

            // Always save as WebP for optimal storage in SQLite
            await image.SaveAsWebpAsync(memoryStream);

            return memoryStream.ToArray();
        }
    }

    public class FileStorageSettings
    {
        public string UploadPath { get; set; } = "wwwroot/uploads";
        public string BaseUrl { get; set; } = "";
        public long MaxFileSize { get; set; } = 5 * 1024 * 1024; // 5MB
        public bool OptimizeImages { get; set; } = true;
        public int MaxImageWidth { get; set; } = 1200;
        public string[] AllowedExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    }
}