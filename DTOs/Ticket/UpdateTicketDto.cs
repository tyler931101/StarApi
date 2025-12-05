using System;
using System.ComponentModel.DataAnnotations;

namespace StarApi.DTOs.Ticket
{
    public class UpdateTicketDto
    {
        [StringLength(150, MinimumLength = 3)]
        public string? Title { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        [RegularExpression(@"^(open|in_progress|resolved|closed)$")]
        public string? Status { get; set; }

        [RegularExpression(@"^(low|medium|high|urgent)$")]
        public string? Priority { get; set; }

        public Guid? AssignedToUserId { get; set; }

        public DateTime? DueDate { get; set; }
    }
}
