using StarApi.DTOs.Auth;
using System.Threading.Tasks;
using StarApi.Models;

namespace StarApi.Services
{
    public interface IAuthService
    {
        Task<StarApi.DTOs.Auth.AuthResponseDto?> RegisterUserAsync(string username, string email, string password);
        Task<bool> VerifyEmailAsync(string token);
        Task<(string? accessToken, string? refreshToken, string? ErrorMessage)> LoginWithTokenAsync(string email, string password);
        Task<(string accessToken, string refreshToken)?> RefreshTokensAsync(string refreshToken);
        Task<bool> InvalidateRefreshTokenAsync(string userId);
        Task<bool> ValidateUserCredentialsAsync(string email, string password);
    }
}
