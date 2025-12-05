using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StarApi.Data;
using StarApi.Models;
using StarApi.Helpers;
using StarApi.DTOs.Auth;
using StarApi.DTOs.User;

namespace StarApi.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            AppDbContext context,
            IConfiguration configuration,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StarApi.DTOs.Auth.AuthResponseDto?> RegisterUserAsync(string username, string email, string password)
        {
            try
            {
                // Input validation
                if (string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogWarning("Registration attempted with empty fields");
                    return null;
                }

                username = username.Trim();
                email = email.Trim().ToLowerInvariant();

                // Check if user already exists
                var existingUser = await _context.Users
                   .Where(u => u.Email == email || u.Username == username)
                   .FirstOrDefaultAsync();

                if (existingUser != null)
                {
                    _logger.LogWarning("Registration attempt with existing credentials: {Email}", email);
                    return null;
                }

                // Create user
                var user = new User
                {
                    Id = Guid.NewGuid(), // Make sure User model has Id property
                    Username = username,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    Role = "User", // Default role
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    IsVerified = false,
                    VerificationToken = GenerateSecureToken(),
                    VerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Send verification email asynchronously
                _ = SendVerificationEmailAsync(user.Email, user.VerificationToken!);

                // Generate JWT token
                var token = JWTHelper.GenerateJwtToken(user, _configuration);

                _logger.LogInformation("User registered successfully: {Email}", email);

                return new StarApi.DTOs.Auth.AuthResponseDto
                {
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        Role = user.Role
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user: {Email}", email);
                throw;
            }
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return false;
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.VerificationToken == token &&
                        u.VerificationTokenExpiry > DateTime.UtcNow);

                if (user == null)
                {
                    _logger.LogWarning("Invalid or expired verification token used");
                    return false;
                }

                user.IsVerified = true;
                user.VerificationToken = null;
                user.VerificationTokenExpiry = null;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Email verified successfully for user: {UserId}", user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email with token: {Token}", token);
                throw;
            }
        }

        public async Task<(string? accessToken, string? refreshToken, string? ErrorMessage)> LoginWithTokenAsync(string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    return (null, null, "Email and password are required.");
                }

                email = email.Trim().ToLowerInvariant();

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                {
                    _logger.LogWarning("Login attempt for non-existent user: {Email}", email);
                    // Generic message for security, but you could say "Invalid credentials"
                    return (null, null, "Invalid email.");
                }

                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    _logger.LogWarning("Invalid password for user: {Email}", email);
                    // Generic message for security
                    return (null, null, "Invalid password.");
                }

                // Check if email is verified
                //if (!user.IsVerified)
                //{
                //    _logger.LogWarning("Login attempt with unverified email: {Email}", email);
                //    // Specific message for unverified email (this is safe to reveal)
                //    return (null, null, "Please verify your email before logging in.");
                //}

                // Check if account is locked/disabled
                if (user.IsLocked)
                {
                    _logger.LogWarning("Login attempt for locked account: {Email}", email);
                    return (null, null, "Your account has been locked. Please contact support.");
                }

                if (user.Status == "Pending")
                {
                    _logger.LogWarning("Login attempt for disabled account: {Email}", email);
                    return (null, null, "Your account is disabled. Please contact support.");
                }

                // Generate tokens
                var accessToken = GenerateJwtToken(user);
                var refreshToken = GenerateRefreshToken();

                // Update user with refresh token
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successful login for user: {Email}", email);
                return (accessToken, refreshToken, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", email);
                throw;
            }
        }

        public async Task<(string accessToken, string refreshToken)?> RefreshTokensAsync(string refreshToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    return null;
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.RefreshToken == refreshToken &&
                        u.RefreshTokenExpiryTime > DateTime.UtcNow);

                if (user == null)
                {
                    _logger.LogWarning("Invalid or expired refresh token used");
                    return null;
                }

                // Generate new tokens
                var newAccessToken = GenerateJwtToken(user);
                var newRefreshToken = GenerateRefreshToken();

                // Update refresh token in database
                user.RefreshToken = newRefreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Tokens refreshed for user: {UserId}", user.Id);
                return (newAccessToken, newRefreshToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing tokens");
                throw;
            }
        }

        public async Task<bool> InvalidateRefreshTokenAsync(string userId)
        {
            try
            {
                if (!Guid.TryParse(userId, out var userGuid))
                {
                    return false;
                }

                var user = await _context.Users.FindAsync(userGuid);
                if (user == null)
                {
                    return false;
                }

                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Refresh token invalidated for user: {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating refresh token for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ValidateUserCredentialsAsync(string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    return false;
                }

                email = email.Trim().ToLowerInvariant();

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                {
                    return false;
                }

                return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating credentials for email: {Email}", email);
                return false;
            }
        }

        #region Private Methods

        private string GenerateJwtToken(User user)
        {
            return JWTHelper.GenerateJwtToken(user, _configuration);
        }

        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private string GenerateSecureToken()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        private async Task SendVerificationEmailAsync(string email, string verificationToken)
        {
            try
            {
                await _emailService.SendVerificationEmailAsync(email, verificationToken);
                _logger.LogInformation("Verification email sent to: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to: {Email}", email);
                // Don't throw - email failure shouldn't break registration
            }
        }

        #endregion
    }
}
