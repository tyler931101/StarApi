using Microsoft.AspNetCore.Http;
using StarApi.DTOs.User;
using StarApi.DTOs.Chat;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StarApi.Services.Interfaces
{
    public interface IChatService
    {
        Task<ChatUserListResponseDto> GetChatUsersAsync(Guid? currentUserId = null);
        Task<ChatMessageDto> SaveMessageAsync(Guid senderId, SendMessageDto message);
        Task<IEnumerable<ChatMessageDto>> GetMessagesAsync(Guid userId1, Guid userId2);
    }
}
