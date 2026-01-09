using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StarApi.Data;
using StarApi.DTOs.User;
using StarApi.Models;
using StarApi.Services.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using BCrypt.Net;

namespace StarApi.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ChatService> _logger;
        private readonly IFileStorageService _fileStorageService;
        private readonly IEmailService _emailService;

        public ChatService(
            AppDbContext context,
            ILogger<ChatService> logger,
            IFileStorageService fileStorageService,
            IEmailService emailService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        // private async Task<string> GetAvatarUrlAsync(Guid userId, IFormFile avatarFile)
        // {
        //     // Check if the service has SaveAvatarToUserAsync method (SQLite storage)
        //     var serviceType = _fileStorageService.GetType();
        //     var saveAvatarMethod = serviceType.GetMethod("SaveAvatarToUserAsync");

        //     if (saveAvatarMethod != null)
        //     {
        //         // Use SQLite storage
        //         try
        //         {
        //             var result = saveAvatarMethod.Invoke(_fileStorageService, new object[] { userId, avatarFile });
        //             if (result is Task<string> task)
        //             {
        //                 return await task;
        //             }
        //         }
        //         catch (Exception ex)
        //         {
        //             _logger.LogError(ex, "Error saving avatar to SQLite");
        //             throw;
        //         }
        //     }

        //     // Fallback to file system
        //     var fileName = $"avatar_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(avatarFile.FileName)}";
        //     var filePath = await _fileStorageService.SaveImageAsync(avatarFile, "avatars", fileName);
        //     return await _fileStorageService.GetFileUrlAsync(filePath);
        // }

        public async Task<(IEnumerable<ChatUserDto> chatUsers, int total)> GetChatUsersAsync()
        {
            var q = _context.Users.AsQueryable();

            var total = await q.CountAsync();

            var chatUsers = await q.Select(u => new ChatUserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
            })
                .ToListAsync();

            return (chatUsers, total);
        }
    }
}
