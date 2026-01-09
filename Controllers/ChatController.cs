using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarApi.Services.Interfaces;
using StarApi.DTOs.User;
using System;
using System.Linq;

namespace StarApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<AuthController> _logger;

        public ChatController(IChatService chatService, ILogger<AuthController> logger)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var (chatUsers, total) = await _chatService.GetChatUsersAsync();
            return Ok(new ChatUserListResponseDto
            {
                ChatUsers = chatUsers.ToList(),
                Total = total,
            });
        }
    }
}
