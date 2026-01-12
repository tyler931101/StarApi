using System.ComponentModel.DataAnnotations;

namespace StarApi.DTOs.Chat
{
    public class SendMessageDto
    {
        [Required]
        public Guid ReceiverId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;
    }
}
