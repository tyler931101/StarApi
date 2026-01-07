using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StarApi.DTOs.Ticket;
using StarApi.DTOs.User;

namespace StarApi.Services.Interfaces
{
    public interface ITicketService
    {
        Task<IEnumerable<TicketDto>> GetTicketsAsync(TicketQueryParamsDto query, Guid currentUserId, bool isAdmin);
        Task<TicketDto?> GetTicketAsync(Guid id, Guid currentUserId, bool isAdmin);
        Task<TicketDto?> CreateTicketAsync(Guid creatorUserId, CreateTicketDto dto);
        Task<TicketDto?> UpdateTicketAsync(Guid id, Guid currentUserId, bool isAdmin, UpdateTicketDto dto);
        Task<bool> DeleteTicketAsync(Guid id, Guid currentUserId, bool isAdmin);

        Task<IEnumerable<UserDto>> GetAssignableUsersAsync(Guid currentUserId, bool isAdmin, string? status = null);

        Task<IEnumerable<TicketStatusCountDto>> GetStatusCountsAsync(Guid currentUserId, bool isAdmin, Guid? createdByUserId, Guid? assignedToUserId);
        Task<int> GetCountByUserAndStatusAsync(Guid currentUserId, bool isAdmin, Guid userId, string relation, string? status);
        Task<IEnumerable<UserTicketCountDto>> GetAssignedUsersByStatusAsync(Guid currentUserId, bool isAdmin, string status);
        Task<AssignedUsersStatusMatrixDto> GetAssignedUsersStatusMatrixAsync(Guid currentUserId, bool isAdmin);
    }
}
