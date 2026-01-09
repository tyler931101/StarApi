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

        [RegularExpression(@"^(todo|in_progress|resolved|testing|closed)$")]
        public string? Status { get; set; }

        [Required]
        [RegularExpression(@"^(low|medium|high|urgent)$")]
        public string Priority { get; set; } = "medium";

        public Guid? AssignedTo { get; set; }

        public DateTime? DueDate { get; set; }
    }
}
