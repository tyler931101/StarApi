using System;
using System.Linq;
using System.Security.Cryptography; // Added for RandomNumberGenerator
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StarApi.Data;
using StarApi.Models;
using StarApi.Helpers;
using StarApi.Services;
using StarApi.DTOs;
using BCrypt.Net;

namespace StarApi.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly EmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, IConfiguration configuration, EmailService emailService, ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<AuthResponseDto?> RegisterUserAsync(string username, string email, string password)
    {
        username = username.Trim();
        email = email.Trim();

        if (_context.Users.Any(u => u.Username == username || u.Email == email))
        {
            return null; // User already exists
        }
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            IsVerified = false,
            VerificationToken = Guid.NewGuid().ToString()
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Send email verification link here (omitted for brevity)
        try
        {
            _ = Task.Run(async () =>
            {
                await _emailService.SendVerificationEmailAsync(user.Email, user.VerificationToken!);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email sending failed for user {Email}", user.Email);
        }

        // Create JWT but only attatch as cookie later
        var token = JWTHelper.GenerateJwtToken(user, _configuration);

        return new AuthResponseDto
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

    public async Task<bool> VerifyEmailAsync(string token)
    {
        var user = await Task.Run(() => _context.Users.FirstOrDefault(u => u.VerificationToken == token));
        if (user == null)
        {
            return false; // Invalid token
        }
        user.IsVerified = true;
        user.VerificationToken = null;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<User?> AuthenticateUserAsync(string email, string password)
    {
        var user = await Task.Run(() => _context.Users.FirstOrDefault(u => u.Email == email));
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null; // Invalid credentials
        }
        //return JWTHelper.GenerateJwtToken(user, _configuration);
        return user;
    }

    public string GenerateJwtToken(User user)
    {
        return JWTHelper.GenerateJwtToken(user, _configuration);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes);
    }

    public async Task<(string access, string refreshToken)> LoginWithTokenAsync(string email, string password)
    {
        var user = await AuthenticateUserAsync(email, password);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var refresh = GenerateRefreshToken();
        user.RefreshToken = refresh;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        var access = GenerateJwtToken(user);
        return (access, refresh);
    }

    public async Task<(string, string)?> RefreshTokensAsync(string refreshToken)
    {
        var user = _context.Users.FirstOrDefault(u => u.RefreshToken == refreshToken);
        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return null; // Invalid refresh token
        }
        var newAccessToken = GenerateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();
        return (newAccessToken, newRefreshToken);
    }
}
