using System.ComponentModel.DataAnnotations;

namespace StarApi.DTOs.User
{
    public class UpdateUserAdminDto
    {
        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9_]+$")]
        public string? Username { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        [StringLength(20)]
        public string? Phone { get; set; }

        [RegularExpression(@"^(admin|user|editor)$", ErrorMessage = "Role must be admin, user, or editor")]
        public string? Role { get; set; }

        [RegularExpression(@"^(active|inactive|pending)$", ErrorMessage = "Status must be active, inactive, or pending")]
        public string? Status { get; set; }
    }
}
