using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarApi.Services.Interfaces;
using StarApi.DTOs.User;
using System;
using System.Linq;

namespace StarApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // Only accessible by users with Admin role
    public class AdminController : ControllerBase
    {
        private readonly IUserService _userService;

        public AdminController(IUserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        [HttpGet("dashboard")]
        public IActionResult GetAdminDashboard()
        {
            return Ok(new
            {
                Message = "Welcome to the Admin Dashboard!",
                Date = DateTime.UtcNow
            });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] UserQueryParamsDto query)
        {
            var (users, total) = await _userService.GetUsersAsync(query);
            var totalPages = (int)Math.Ceiling((double)total / Math.Max(query.PageSize, 1));
            return Ok(new UserListResponseDto
            {
                Users = users.ToList(),
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalPages = totalPages
            });
        }

        [HttpGet("user/{id}")]
        public async Task<IActionResult> GetUser(Guid id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null) return NotFound();
            return Ok(user);
        }



        [HttpPut("user/{id}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserAdminDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new { success = false, message = "Update data is required" });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .SelectMany(x => x.Value!.Errors.Select(e => new { field = x.Key, message = e.ErrorMessage }))
                        .ToList();
                    return BadRequest(new { success = false, message = "Validation failed", errors });
                }

                var updated = await _userService.UpdateUserAsync(id, dto);
                if (updated == null) return NotFound(new { success = false, message = "User not found" });
                return Ok(new { success = true, data = updated });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while updating the user" });
            }
        }

        [HttpDelete("user/{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var ok = await _userService.DeleteUserAsync(id);
            return ok ? NoContent() : NotFound();
        }
    }
}
