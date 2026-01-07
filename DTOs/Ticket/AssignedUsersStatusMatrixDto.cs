using System.Collections.Generic;

namespace StarApi.DTOs.Ticket
{
    public class AssignedUsersStatusMatrixDto
    {
        public IEnumerable<UserTicketCountDto> Open { get; set; } = new List<UserTicketCountDto>();
        public IEnumerable<UserTicketCountDto> In_Progress { get; set; } = new List<UserTicketCountDto>();
        public IEnumerable<UserTicketCountDto> Resolved { get; set; } = new List<UserTicketCountDto>();
        public IEnumerable<UserTicketCountDto> Testing { get; set; } = new List<UserTicketCountDto>();
        public IEnumerable<UserTicketCountDto> Closed { get; set; } = new List<UserTicketCountDto>();
    }
}
