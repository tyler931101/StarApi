using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StarApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // Only accessible by users with Admin role
    public class AdminController : ControllerBase
    {
        [HttpGet("dashboard")]
        public IActionResult GetAdminDashboard()
        {
            return Ok(new
            {
                Message = "Welcome to the Admin Dashboard!",
                Date = DateTime.UtcNow
            });
        }
    }
}