using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StarApi.Data;
using StarApi.DTOs.Chat;
using StarApi.DTOs.User;
using StarApi.Models;
using StarApi.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace StarApi.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            AppDbContext context,
            ILogger<ChatService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ChatMessageDto> SaveMessageAsync(Guid senderId, SendMessageDto message)
        {
            var chatMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SenderId = senderId,
                ReceiverId = message.ReceiverId,
                Content = message.Content,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            return new ChatMessageDto
            {
                Id = chatMessage.Id,
                SenderId = chatMessage.SenderId,
                ReceiverId = chatMessage.ReceiverId,
                Content = chatMessage.Content,
                CreatedAt = chatMessage.CreatedAt,
                IsRead = chatMessage.IsRead
            };
        }

        public async Task<IEnumerable<ChatMessageDto>> GetMessagesAsync(Guid userId1, Guid userId2)
        {
            var messages = await _context.ChatMessages
                .Where(m => (m.SenderId == userId1 && m.ReceiverId == userId2) ||
                            (m.SenderId == userId2 && m.ReceiverId == userId1))
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    ReceiverId = m.ReceiverId,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    IsRead = m.IsRead
                })
                .ToListAsync();

            return messages;
        }

        public async Task<ChatUserListResponseDto> GetChatUsersAsync(Guid? currentUserId = null)
    {
        var query = _context.Users.AsQueryable();
        
        // Filter out current user if provided
        if (currentUserId.HasValue)
        {
            query = query.Where(u => u.Id != currentUserId.Value);
        }
        
        var total = await query.CountAsync();
        var chatUsers = await query.Select(u => new ChatUserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            AvatarUrl = u.AvatarUrl,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt,
        })
        .ToListAsync();
        return new ChatUserListResponseDto
        {
            ChatUsers = chatUsers,
            Total = total
        };
    }
    }
}
