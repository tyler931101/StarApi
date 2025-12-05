using System.ComponentModel.DataAnnotations;

namespace StarApi.DTOs.Auth
{
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}