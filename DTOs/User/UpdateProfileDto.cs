using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace StarApi.DTOs.User
{
    public class UpdateProfileDto
    {
        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
        public string? Username { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Invalid phone number")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string? Phone { get; set; }
    }

    public class UploadAvatarDto
    {
        [Required(ErrorMessage = "Image file is required")]
        public IFormFile? ImageFile { get; set; }
    }

    public class UpdateProfileWithAvatarDto
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public IFormFile? Avatar { get; set; }
    }

    public class ProfileUpdateResultDto
    {
        public UserProfileDto Profile { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool RequiresEmailVerification { get; set; }
    }
}