using Microsoft.AspNetCore.Http;
using StarApi.DTOs.User;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StarApi.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
        Task<UserProfileDto?> UpdateUserProfileAsync(Guid userId, UpdateProfileDto updateDto);
        Task<ProfileUpdateResultDto?> UpdateProfileWithAvatarAsync(Guid userId, UpdateProfileDto profileDto, IFormFile? avatarFile);
        Task<string?> UploadAvatarAsync(Guid userId, IFormFile imageFile);
        Task<bool> DeleteAvatarAsync(Guid userId);
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto);
        Task<(string email, bool isVerified)> GetEmailVerificationStatusAsync(Guid userId);
        Task<bool> ResendVerificationEmailAsync(Guid userId);
        Task<bool> IsUserActiveAsync(Guid userId);
        Task<UserActivityDto?> GetUserActivityAsync(Guid userId);

        // Admin operations
        Task<(IEnumerable<UserDto> users, int total)> GetUsersAsync(UserQueryParamsDto query);
        Task<UserDto?> GetUserByIdAsync(Guid id);
        Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserAdminDto dto);
        Task<bool> DeleteUserAsync(Guid id);
    }
}
