using System;
using System.ComponentModel.DataAnnotations;

namespace StarApi.Models
{
    public class Ticket
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Open";

        [Required]
        [MaxLength(20)]
        public string Priority { get; set; } = "Medium";

        public Guid CreatedByUserId { get; set; }
        public Guid? AssignedTo { get; set; }

        public User CreatedByUser { get; set; } = null!;
        public User? AssignedToUser { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
