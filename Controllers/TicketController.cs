using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarApi.DTOs.Ticket;
using StarApi.Services.Interfaces;

namespace StarApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TicketController : ControllerBase
    {
        private readonly ITicketService _ticketService;

        public TicketController(ITicketService ticketService)
        {
            _ticketService = ticketService;
        }

        [HttpGet("assignes")]
        public async Task<IActionResult> GetAssignes([FromQuery] string? status)
        {
            var (userId, isAdmin) = GetContext();
            var users = await _ticketService.GetAssignableUsersAsync(userId, isAdmin, status);
            return Ok(new { data = new { users } });
        }

        [HttpGet]
        public async Task<IActionResult> GetTickets([FromQuery] TicketQueryParamsDto query)
        {
            var (userId, isAdmin) = GetContext();
            var items = await _ticketService.GetTicketsAsync(query, userId, isAdmin);
            return Ok(new
            {
                users = items
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTicket(Guid id)
        {
            var (userId, isAdmin) = GetContext();
            var t = await _ticketService.GetTicketAsync(id, userId, isAdmin);
            if (t == null) return NotFound();
            return Ok(t);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateTicketDto dto)
        {
            var (userId, isAdmin) = GetContext();
            if (!isAdmin) return Forbid();
            var created = await _ticketService.CreateTicketAsync(userId, dto);
            return CreatedAtAction(nameof(GetTicket), new { id = created!.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTicketDto dto)
        {
            var (userId, isAdmin) = GetContext();
            var updated = await _ticketService.UpdateTicketAsync(id, userId, isAdmin, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var (userId, isAdmin) = GetContext();
            var ok = await _ticketService.DeleteTicketAsync(id, userId, isAdmin);
            return ok ? NoContent() : NotFound();
        }

        [HttpGet("report/status")]
        public async Task<IActionResult> GetStatusReport([FromQuery] Guid? createdByUserId, [FromQuery] Guid? assignedTo)
        {
            var (userId, isAdmin) = GetContext();
            var items = await _ticketService.GetStatusCountsAsync(userId, isAdmin, createdByUserId, assignedTo);
            var total = items.Sum(x => x.Count);
            var byStatus = new
            {
                open = items.FirstOrDefault(x => x.Status == "open")?.Count ?? 0,
                in_progress = items.FirstOrDefault(x => x.Status == "in_progress")?.Count ?? 0,
                resolved = items.FirstOrDefault(x => x.Status == "resolved")?.Count ?? 0,
                closed = items.FirstOrDefault(x => x.Status == "closed")?.Count ?? 0
            };
            return Ok(new { counts = items, total, byStatus });
        }

        [HttpGet("report/user-status")]
        public async Task<IActionResult> GetCountByUserAndStatus([FromQuery] Guid userId, [FromQuery] string relation = "created", [FromQuery] string? status = null)
        {
            var (currentUserId, isAdmin) = GetContext();
            var count = await _ticketService.GetCountByUserAndStatusAsync(currentUserId, isAdmin, userId, relation, status);
            return Ok(new { count });
        }

        [HttpGet("report/assigned-users")]
        public async Task<IActionResult> GetAssignedUsersByStatus([FromQuery] string status)
        {
            var (currentUserId, isAdmin) = GetContext();
            var items = await _ticketService.GetAssignedUsersByStatusAsync(currentUserId, isAdmin, status);
            var total = items.Sum(x => x.Count);
            return Ok(new { status = status.ToLower(), items, total });
        }

        [HttpGet("report/assigned-users-matrix")]
        public async Task<IActionResult> GetAssignedUsersStatusMatrix()
        {
            var (currentUserId, isAdmin) = GetContext();
            var matrix = await _ticketService.GetAssignedUsersStatusMatrixAsync(currentUserId, isAdmin);
            return Ok(new { matrix });
        }

        private (Guid userId, bool isAdmin) GetContext()
        {
            var idClaim = User.FindFirst("id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(idClaim, out var uid);
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var isAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Equals("admin", StringComparison.OrdinalIgnoreCase);
            return (uid, isAdmin);
        }
    }
}
