using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StarApi.Services.Interfaces;
using StarApi.DTOs.Chat;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StarApi.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(IChatService chatService, ILogger<ChatHub> logger)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (userId != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
                _logger.LogInformation($"User {userId} connected with connection {Context.ConnectionId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (userId != null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
                _logger.LogInformation($"User {userId} disconnected from connection {Context.ConnectionId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(SendMessageDto message)
        {
            var senderIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(senderIdStr) || !Guid.TryParse(senderIdStr, out var senderId))
            {
                throw new HubException("Unauthorized");
            }

            var savedMessage = await _chatService.SaveMessageAsync(senderId, message);

            // Send to receiver's group
            await Clients.Group($"User_{message.ReceiverId}").SendAsync("ReceiveMessage", savedMessage);
            
            // Send back to sender's group
            await Clients.Group($"User_{senderId}").SendAsync("ReceiveMessage", savedMessage);
        }
    }
}
