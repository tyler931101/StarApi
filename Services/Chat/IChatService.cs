using Microsoft.AspNetCore.Http;
using StarApi.DTOs.User;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StarApi.Services.Interfaces
{
    public interface IChatService
    {
        Task<(IEnumerable<ChatUserDto> chatUsers, int total)> GetChatUsersAsync();
    }
}
