using System;
using System.ComponentModel.DataAnnotations;

namespace StarApi.DTOs.Ticket
{
    public class TicketQueryParamsDto
    {
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public Guid? AssignedTo { get; set; }
        public Guid? CreatedByUserId { get; set; }
    }
}
