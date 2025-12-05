using System;

namespace StarApi.DTOs.Ticket
{
    public class UserTicketCountDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
