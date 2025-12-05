using System;

namespace StarApi.DTOs.Ticket
{
    public class TicketDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public Guid CreatedByUserId { get; set; }
        public string? CreatedByUsername { get; set; }
        public string? CreatedByEmail { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public string? AssignedToUsername { get; set; }
        public string? AssignedToEmail { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
