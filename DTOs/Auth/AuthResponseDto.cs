using StarApi.DTOs.User;

namespace StarApi.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? RefreshToken { get; set; }
        public UserDto User { get; set; } = new();
    }
}
