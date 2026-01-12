using System.Collections.Generic;
using StarApi.DTOs.Chat;

namespace StarApi.DTOs.User
{
    public class ChatUserListResponseDto
    {
        public List<ChatUserDto> ChatUsers { get; set; } = new List<ChatUserDto>();
        public int Total { get; set; }
    }
}