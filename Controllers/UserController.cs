using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarApi.Data;

namespace StarApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // requires valid JWT

    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        public UserController(AppDbContext context)
        {
            _context = context;
        }
        [HttpGet("profile")]
        public IActionResult GetUserProfile()
        {
            var userId = User.Claims.First(c => c.Type == "id").Value;
            var user = _context.Users.Find(int.Parse(userId));
            if (user == null)
            {
                return NotFound();
            }
            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.CreatedAt
            });
        }
    }
}
