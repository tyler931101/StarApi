using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarApi.Services.Interfaces;
using StarApi.DTOs.Chat;
using StarApi.DTOs.User;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StarApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IChatService chatService,
            ILogger<ChatController> logger)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // REST API endpoints
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? currentUserId = null;
            if (!string.IsNullOrEmpty(currentUserIdStr) && Guid.TryParse(currentUserIdStr, out var parsedId))
            {
                currentUserId = parsedId;
            }

            var chatUserListResponse = await _chatService.GetChatUsersAsync(currentUserId);
            return Ok(chatUserListResponse);
        }

        [Authorize]
        [HttpGet("messages/{userId}")]
        public async Task<IActionResult> GetMessages(Guid userId)
        {
            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdStr) || !Guid.TryParse(currentUserIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            var messages = await _chatService.GetMessagesAsync(currentUserId, userId);
            return Ok(messages);
        }

        [Authorize]
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto message)
        {
            var senderIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(senderIdStr) || !Guid.TryParse(senderIdStr, out var senderId))
            {
                return Unauthorized();
            }

            try
            {
                var savedMessage = await _chatService.SaveMessageAsync(senderId, message);
                return Ok(new { success = true, messageId = savedMessage.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { success = false, error = "Failed to send message" });
            }
        }
    }
}
