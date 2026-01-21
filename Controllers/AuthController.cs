using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StarApi.DTOs.User;
using StarApi.DTOs.Auth;
using StarApi.Models;
using StarApi.Services;
using System;
using System.Threading.Tasks;

namespace StarApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new { Message = "Registration data is required." });
                }

                var validationResult = ValidateRegistrationData(dto);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new { Message = validationResult.ErrorMessage });
                }

                var result = await _authService.RegisterUserAsync(dto.Username, dto.Email, dto.Password, dto.Id);

                if (result == null)
                {
                    return Conflict(new { Message = "User with this email or username already exists." });
                }

                // Set JWT as HttpOnly cookie for security
                Response.Cookies.Append("jwt", result.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true, // Ensure this is true in production
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddHours(1)
                });

                _logger.LogInformation("User registered successfully: {Email}", dto.Email);

                return Ok(new
                {
                    Message = "Registration successful. Please check your email for verification.",
                    User = result.User
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed for email: {Email}", dto?.Email);
                return StatusCode(500, new { Message = "An error occurred during registration." });
            }
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return BadRequest(new { Message = "Verification token is required." });
                }

                var success = await _authService.VerifyEmailAsync(token);

                if (!success)
                {
                    return BadRequest(new { Message = "Invalid or expired verification token." });
                }

                _logger.LogInformation("Email verified successfully for token: {Token}", token);
                return Ok(new { Message = "Email verified successfully. You can now log in." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email verification failed for token: {Token}", token);
                return StatusCode(500, new { Message = "An error occurred during email verification." });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new { Message = "Login credentials are required." });
                }

                var validationResult = ValidateLoginData(dto);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new { Message = validationResult.ErrorMessage });
                }

                var result = await _authService.LoginWithTokenAsync(dto.Email, dto.Password);

                if (result.accessToken == null || result.refreshToken == null)
                {
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        // Return specific error message from service
                        return Unauthorized(new { Message = result.ErrorMessage });
                    }

                    // Fallback to generic message
                    _logger.LogWarning("Failed login attempt for email: {Email}", dto.Email);
                    return Unauthorized(new { Message = "Invalid credentials." });
                }

                _logger.LogInformation("User logged in successfully: {Email}", dto.Email);

                return Ok(new
                {
                    AccessToken = result.accessToken,
                    RefreshToken = result.refreshToken,
                    ExpiresIn = 7200 // Token expiration in seconds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for email: {Email}", dto?.Email);
                return StatusCode(500, new { Message = "An error occurred during login." });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    return BadRequest(new { Message = "Refresh token is required." });
                }

                var result = await _authService.RefreshTokensAsync(request.RefreshToken);

                if (result == null)
                {
                    return Unauthorized(new { Message = "Invalid or expired refresh token." });
                }

                return Ok(new
                {
                    AccessToken = result.Value.accessToken,
                    RefreshToken = result.Value.refreshToken,
                    ExpiresIn = 3600
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh failed");
                return StatusCode(500, new { Message = "An error occurred while refreshing tokens." });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                // Get user ID from token (you'll need to extract this from JWT)
                var userId = User.FindFirst("id")?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    await _authService.InvalidateRefreshTokenAsync(userId);
                }

                // Clear the JWT cookie
                Response.Cookies.Delete("jwt", new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                });

                _logger.LogInformation("User logged out: {UserId}", userId);
                return Ok(new { Message = "Logged out successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed");
                return StatusCode(500, new { Message = "An error occurred during logout." });
            }
        }

        #region Private Methods

        private (bool IsValid, string ErrorMessage) ValidateRegistrationData(RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || dto.Username.Length < 3)
            {
                return (false, "Username must be at least 3 characters long.");
            }

            if (string.IsNullOrWhiteSpace(dto.Email) || !IsValidEmail(dto.Email))
            {
                return (false, "A valid email address is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
            {
                return (false, "Password must be at least 8 characters long.");
            }

            return (true, string.Empty);
        }

        private (bool IsValid, string ErrorMessage) ValidateLoginData(LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || !IsValidEmail(dto.Email))
            {
                return (false, "A valid email address is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Password))
            {
                return (false, "Password is required.");
            }

            return (true, string.Empty);
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
