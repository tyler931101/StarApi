using System;
using System.ComponentModel.DataAnnotations;

namespace StarApi.DTOs.Ticket
{
    public class CreateTicketDto
    {
        [Required]
        [StringLength(150, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        [Required]
        [RegularExpression(@"^(low|medium|high|urgent)$")]
        public string Priority { get; set; } = "medium";

        [Required]
        [RegularExpression(@"^(todo|in_progress|resolved|testing|done)$")]
        public string Status { get; set; } = "todo";

        public Guid? AssignedTo { get; set; }

        public DateTime? DueDate { get; set; }
    }
}
