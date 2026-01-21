using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using StarApi.Data;
using StarApi.DTOs.Ticket;
using StarApi.Services.Interfaces;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace StarApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly ITicketService _ticketService;
        private readonly IUserService _userService;

        public SyncController(IConfiguration configuration, AppDbContext context, ITicketService ticketService, IUserService userService)
        {
            _configuration = configuration;
            _context = context;
            _ticketService = ticketService;
            _userService = userService;
        }

        [HttpPost("ticket")]
        public async Task<IActionResult> CreateTicket([FromBody] CreateTicketDto dto)
        {
            var key = Request.Headers["X-Integration-Key"].ToString();
            var expectedKey = _configuration["Integration:Key"];
            if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(key, expectedKey, StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "invalid_integration_key" });
            }

            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Role.ToLower() == "admin");
            if (admin == null)
            {
                return BadRequest(new { error = "no_admin_user_available" });
            }

            if (dto.AssignedTo.HasValue)
            {
                var exists = await _context.Users.AnyAsync(u => u.Id == dto.AssignedTo.Value);
                if (!exists)
                {
                    dto.AssignedTo = null;
                }
            }

            var created = await _ticketService.CreateTicketAsync(admin.Id, dto);
            if (created == null) return BadRequest(new { error = "create_failed" });

            return CreatedAtAction(nameof(CreateTicket), new { id = created.Id }, created);
        }

        [HttpPut("ticket/{id:guid}")]
        public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] UpdateTicketDto dto)
        {
            var key = Request.Headers["X-Integration-Key"].ToString();
            var expectedKey = _configuration["Integration:Key"];
            if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(key, expectedKey, StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "invalid_integration_key" });
            }

            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Role.ToLower() == "admin");
            if (admin == null) return BadRequest(new { error = "no_admin_user_available" });

            var updated = await _ticketService.UpdateTicketAsync(id, admin.Id, true, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpPatch("ticket/{id:guid}/move")]
        public async Task<IActionResult> MoveTicket(Guid id, [FromBody] UpdateTicketDto body)
        {
            var key = Request.Headers["X-Integration-Key"].ToString();
            var expectedKey = _configuration["Integration:Key"];
            if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(key, expectedKey, StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "invalid_integration_key" });
            }

            if (string.IsNullOrWhiteSpace(body.Status)) return BadRequest(new { error = "status is required" });

            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Role.ToLower() == "admin");
            if (admin == null) return BadRequest(new { error = "no_admin_user_available" });

            var updated = await _ticketService.MoveTicketAsync(id, admin.Id, true, body.Status!);
            if (updated == null) return NotFound();
            return Ok(new { data = updated });
        }

        [HttpDelete("ticket/{id:guid}")]
        public async Task<IActionResult> DeleteTicket(Guid id)
        {
            var key = Request.Headers["X-Integration-Key"].ToString();
            var expectedKey = _configuration["Integration:Key"];
            if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(key, expectedKey, StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "invalid_integration_key" });
            }

            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Role.ToLower() == "admin");
            if (admin == null) return BadRequest(new { error = "no_admin_user_available" });

            var ok = await _ticketService.DeleteTicketAsync(id, admin.Id, true);
            return ok ? NoContent() : NotFound();
        }

        [HttpPost("users/{id:guid}/avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadUserAvatar(Guid id, [FromForm] IFormFile file)
        {
            var key = Request.Headers["X-Integration-Key"].ToString();
            var expectedKey = _configuration["Integration:Key"];
            if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(key, expectedKey, StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "invalid_integration_key" });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "no_file" });
            }

            try
            {
                var url = await _userService.UploadAvatarAsync(id, file);
                if (url == null)
                {
                    return NotFound(new { error = "user_not_found" });
                }
                return Ok(new { success = true, avatarUrl = url });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "server_error" });
            }
        }
    }
}
