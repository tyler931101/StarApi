using System;
using System.ComponentModel.DataAnnotations;

namespace StarApi.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Keep AvatarUrl for backward compatibility (stores URL pattern)
        public string AvatarUrl { get; set; } = string.Empty;

        // Add these for SQLite avatar storage
        public byte[]? AvatarData { get; set; }
        public string? AvatarMimeType { get; set; }
        public DateTime? AvatarUpdatedAt { get; set; }

        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "User";

        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }

        public bool IsVerified { get; set; }
        public string? VerificationToken { get; set; }
        public DateTime? VerificationTokenExpiry { get; set; }

        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        public bool IsLocked { get; set; } = false;
        public bool IsDisabled { get; set; } = false;
    }
}