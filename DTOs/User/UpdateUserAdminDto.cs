using System.ComponentModel.DataAnnotations;

namespace StarApi.DTOs.User
{
    public class UpdateUserAdminDto
    {
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
        public string? Username { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string? Email { get; set; }

        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string? Phone { get; set; }

        [RegularExpression(@"^(admin|user|editor)$", ErrorMessage = "Role must be admin, user, or editor")]
        public string? Role { get; set; }

        [RegularExpression(@"^(active|inactive|pending)$", ErrorMessage = "Status must be active, inactive, or pending")]
        public string? Status { get; set; }
    }
}
