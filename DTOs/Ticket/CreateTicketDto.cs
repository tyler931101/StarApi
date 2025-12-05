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

        public Guid? AssignedToUserId { get; set; }

        public DateTime? DueDate { get; set; }
    }
}
