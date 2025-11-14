using Microsoft.EntityFrameworkCore;
using StarApi.Models;
using StarApi.Services;
using Microsoft.AspNetCore.Mvc;
using StarApi.DTOs;

namespace StarApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            var result = await _authService.RegisterUserAsync(dto.Username, dto.Email, dto.Password);
            if (result == null)
            {
                return BadRequest("User already exists.");
            }

            // Set JWT as HttpOnly cookie for security
            Response.Cookies.Append("jwt", result.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            });

            // Do not return the token in the response body to the frontend
            return Ok(new
            {
                message = "Registration successful.",
                user = result.User
            });
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            var success = await _authService.VerifyEmailAsync(token);
            if (!success)
            {
                return BadRequest("Invalid or expired verification token.");
            }
            return Ok("Email verified successfully.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var (access, refreshToken) = await _authService.LoginWithTokenAsync(dto.Email, dto.Password);
            if(access == null)
            {
                return Unauthorized("Invalid credentials.");
            }
            return Ok(new { AccessToken = access, RefreshToken = refreshToken });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] string refreshToken)
        {
            var result = await _authService.RefreshTokensAsync(refreshToken);
            if(result == null)
            {
                return Unauthorized("Invalid refresh token.");
            }

            (string access, string newRefresh) = result.Value;
            return Ok(new { AccessToken = access, RefreshToken = refreshToken });
        }
    }
}
