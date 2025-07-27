using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Domain.Entities;

namespace Frigorino.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ApplicationDbContext _context;

        public AuthController(ICurrentUserService currentUserService, ApplicationDbContext context)
        {
            _currentUserService = currentUserService;
            _context = context;
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId) || userId == "0")
            {
                return Unauthorized("User not authenticated");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.ExternalId == userId);
            
            if (user == null)
            {
                // Create user if doesn't exist
                user = new User
                {
                    ExternalId = userId,
                    Name = "New User"
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            return Ok(new { user.ExternalId, user.Name });
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId) || userId == "0")
            {
                return Unauthorized("User not authenticated");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.ExternalId == userId);
            
            if (user == null)
            {
                return NotFound("User not found");
            }

            user.Name = request.Name;
            await _context.SaveChangesAsync();

            return Ok(new { user.ExternalId, user.Name });
        }
    }

    public class UpdateProfileRequest
    {
        public string Name { get; set; } = string.Empty;
    }
}
