using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StarApi.DTOs.User;
using StarApi.Services.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace StarApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;
        private readonly IFileStorageService _fileStorageService;

        public UserController(
            IUserService userService,
            ILogger<UserController> logger,
            IFileStorageService fileStorageService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "Invalid token" });
                }

                var profile = await _userService.GetUserProfileAsync(userId);
                if (profile == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                return Ok(new
                {
                    success = true,
                    message = "Profile retrieved",
                    data = profile
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return StatusCode(500, new { success = false, message = "Error retrieving profile" });
            }
        }

        // NEW: Combined update endpoint
        [HttpPut("update-profile")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProfileWithAvatar(
            [FromForm] string? username,
            [FromForm] string? email,
            [FromForm] string? phone,
            [FromForm] IFormFile? avatar)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "Invalid token" });
                }

                var profileDto = new UpdateProfileDto
                {
                    Username = username?.Trim(),
                    Email = email?.Trim(),
                    Phone = phone?.Trim()
                };

                var result = await _userService.UpdateProfileWithAvatarAsync(userId, profileDto, avatar);

                if (result == null)
                {
                    return BadRequest(new { success = false, message = "Failed to update profile" });
                }

                var response = new
                {
                    success = true,
                    message = "Profile updated successfully" +
                             (result.RequiresEmailVerification ?
                              ". Please verify your new email." : ""),
                    data = new
                    {
                        profile = result.Profile,
                        avatarUrl = result.AvatarUrl,
                        requiresEmailVerification = result.RequiresEmailVerification
                    }
                };

                return Ok(response);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already"))
            {
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile with avatar");
                return StatusCode(500, new { success = false, message = "Error updating profile" });
            }
        }

        // Remove old update-profile endpoint or keep as backup
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "Invalid token" });
                }

                if (dto == null)
                {
                    return BadRequest(new { success = false, message = "No data provided" });
                }

                var updatedProfile = await _userService.UpdateUserProfileAsync(userId, dto);
                if (updatedProfile == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                return Ok(new
                {
                    success = true,
                    message = "Profile updated",
                    data = updatedProfile
                });
            }
            catch (InvalidOperationException ex) when (ex.Message == "Username is already taken")
            {
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                return StatusCode(500, new { success = false, message = "Error updating profile" });
            }
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "Invalid token" });
                }

                if (dto == null)
                {
                    return BadRequest(new { success = false, message = "No data provided" });
                }

                // Validate model
                if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                {
                    return BadRequest(new { success = false, message = "Current password is required" });
                }

                if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
                {
                    return BadRequest(new { success = false, message = "New password must be at least 8 characters" });
                }

                if (dto.NewPassword != dto.ConfirmPassword)
                {
                    return BadRequest(new { success = false, message = "New passwords do not match" });
                }

                var success = await _userService.ChangePasswordAsync(userId, dto);
                if (!success)
                {
                    return BadRequest(new { success = false, message = "Unable to change password. Please check your current password and try again." });
                }

                return Ok(new { success = true, message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return StatusCode(500, new { success = false, message = "Error changing password" });
            }
        }

        // Keep avatar endpoints for standalone use
        [HttpPost("upload-avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar([FromForm] UploadAvatarDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "Invalid token" });
                }

                if (dto?.ImageFile == null)
                {
                    return BadRequest(new { success = false, message = "No image file" });
                }

                var avatarUrl = await _userService.UploadAvatarAsync(userId, dto.ImageFile);
                if (avatarUrl == null)
                {
                    return BadRequest(new { success = false, message = "Failed to upload avatar" });
                }

                return Ok(new
                {
                    success = true,
                    message = "Avatar uploaded",
                    data = new { avatarUrl }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar");
                return StatusCode(500, new { success = false, message = "Error uploading avatar" });
            }
        }

        [HttpDelete("avatar")]
        public async Task<IActionResult> DeleteAvatar()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "Invalid token" });
                }

                var success = await _userService.DeleteAvatarAsync(userId);
                if (!success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Failed to delete avatar. User not found or no avatar exists."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Avatar deleted successfully",
                    timestamp = DateTime.UtcNow // Useful for cache busting
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting avatar");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error deleting avatar",
                    detail = ex.Message
                });
            }
        }

        private Guid GetCurrentUserId()
        {
            try
            {
                var idClaim = User.FindFirst("id");
                if (idClaim != null && Guid.TryParse(idClaim.Value, out Guid userId))
                {
                    return userId;
                }

                var nameIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (nameIdClaim != null && Guid.TryParse(nameIdClaim.Value, out Guid nameId))
                {
                    return nameId;
                }

                return Guid.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user ID");
                return Guid.Empty;
            }
        }

        [HttpGet("{userId}/avatar")]
        [AllowAnonymous]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> GetUserAvatar(Guid userId)
        {
            try
            {
                // Use the service method that already exists
                var avatar = await _fileStorageService.GetUserAvatarAsync(userId);
                return File(avatar.data, avatar.mimeType);
            }
            catch (FileNotFoundException)
            {
                // Return default avatar
                var defaultAvatarPath = Path.Combine(Directory.GetCurrentDirectory(),
                    "wwwroot", "images", "default-avatar.png");

                if (System.IO.File.Exists(defaultAvatarPath))
                {
                    return PhysicalFile(defaultAvatarPath, "image/png");
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving avatar for user {UserId}", userId);
                return StatusCode(500, "Error retrieving avatar");
            }
        }
    }
}