using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;

namespace Frigorino.Web.Controllers;

[ApiController]
[Route("api/household/{householdId}/[controller]")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MembersController(ApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all members of a household
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<HouseholdMemberDto>>> GetHouseholdMembers(int householdId)
    {
        var userId = _currentUserService.UserId;
        
        // Check if user has access to this household
        var userAccess = await _context.UserHouseholds
            .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive);

        if (!userAccess)
        {
            return NotFound("Household not found or you don't have access to it.");
        }

        var members = await _context.UserHouseholds
            .Where(uh => uh.HouseholdId == householdId && uh.IsActive)
            .Include(uh => uh.User)
            .Select(uh => new HouseholdMemberDto
            {
                User = new UserDto
                {
                    ExternalId = uh.User.ExternalId,
                    Name = uh.User.Name,
                    Email = uh.User.Email
                },
                Role = uh.Role,
                JoinedAt = uh.JoinedAt
            })
            .OrderByDescending(m => m.Role) // Owners first, then Admins, then Members
            .ThenBy(m => m.JoinedAt)
            .ToListAsync();

        return Ok(members);
    }

    /// <summary>
    /// Add a user to household by email (Admin/Owner only)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<HouseholdMemberDto>> AddMember(int householdId, AddMemberRequest request)
    {
        var userId = _currentUserService.UserId;
        
        // Check if current user has admin/owner permissions
        var currentUserRole = await _context.UserHouseholds
            .Where(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive)
            .Select(uh => uh.Role)
            .FirstOrDefaultAsync();

        if (currentUserRole == null)
        {
            return NotFound("Household not found or you don't have access to it.");
        }

        if (currentUserRole == HouseholdRole.Member)
        {
            return Forbid("You don't have permission to add members to this household.");
        }

        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Email is required.");
        }

        var email = request.Email.Trim().ToLowerInvariant();

        // Find user by email
        var targetUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.IsActive);

        if (targetUser == null)
        {
            return BadRequest("User with this email not found.");
        }

        // Check if user is already a member
        var existingMembership = await _context.UserHouseholds
            .FirstOrDefaultAsync(uh => uh.UserId == targetUser.ExternalId && uh.HouseholdId == householdId);

        if (existingMembership != null)
        {
            if (existingMembership.IsActive)
            {
                return BadRequest("User is already a member of this household.");
            }
            else
            {
                // Reactivate existing membership
                existingMembership.IsActive = true;
                existingMembership.Role = request.Role ?? HouseholdRole.Member;
                existingMembership.JoinedAt = DateTime.UtcNow;
            }
        }
        else
        {
            // Create new membership
            var newMembership = new UserHousehold
            {
                UserId = targetUser.ExternalId,
                HouseholdId = householdId,
                Role = request.Role ?? HouseholdRole.Member,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.UserHouseholds.Add(newMembership);
        }

        await _context.SaveChangesAsync();

        // Return the new member info
        var memberInfo = new HouseholdMemberDto
        {
            User = new UserDto
            {
                ExternalId = targetUser.ExternalId,
                Name = targetUser.Name,
                Email = targetUser.Email
            },
            Role = request.Role ?? HouseholdRole.Member,
            JoinedAt = DateTime.UtcNow
        };

        return CreatedAtAction(nameof(GetHouseholdMembers), new { householdId }, memberInfo);
    }

    /// <summary>
    /// Update a member's role (Admin/Owner only)
    /// </summary>
    [HttpPut("{memberUserId}")]
    public async Task<ActionResult<HouseholdMemberDto>> UpdateMemberRole(int householdId, string memberUserId, UpdateMemberRoleRequest request)
    {
        var userId = _currentUserService.UserId;
        
        // Check if current user has admin/owner permissions
        var currentUserRole = await _context.UserHouseholds
            .Where(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive)
            .Select(uh => uh.Role)
            .FirstOrDefaultAsync();

        if (currentUserRole == null)
        {
            return NotFound("Household not found or you don't have access to it.");
        }

        if (currentUserRole == HouseholdRole.Member)
        {
            return Forbid("You don't have permission to update member roles.");
        }

        // Find the target member
        var targetMembership = await _context.UserHouseholds
            .Include(uh => uh.User)
            .FirstOrDefaultAsync(uh => uh.UserId == memberUserId && uh.HouseholdId == householdId && uh.IsActive);

        if (targetMembership == null)
        {
            return NotFound("Member not found in this household.");
        }

        // Prevent changing owner role (only owner can transfer ownership)
        if (targetMembership.Role == HouseholdRole.Owner && currentUserRole != HouseholdRole.Owner)
        {
            return Forbid("Only the owner can change the owner's role.");
        }

        // Prevent non-owners from creating new owners
        if (request.Role == HouseholdRole.Owner && currentUserRole != HouseholdRole.Owner)
        {
            return Forbid("Only the owner can assign ownership to others.");
        }

        // Update role
        targetMembership.Role = request.Role;
        await _context.SaveChangesAsync();

        // Return updated member info
        var memberInfo = new HouseholdMemberDto
        {
            User = new UserDto
            {
                ExternalId = targetMembership.User.ExternalId,
                Name = targetMembership.User.Name,
                Email = targetMembership.User.Email
            },
            Role = targetMembership.Role,
            JoinedAt = targetMembership.JoinedAt
        };

        return Ok(memberInfo);
    }

    /// <summary>
    /// Remove a member from household (Admin/Owner only, or self-removal)
    /// </summary>
    [HttpDelete("{memberUserId}")]
    public async Task<ActionResult> RemoveMember(int householdId, string memberUserId)
    {
        var userId = _currentUserService.UserId;
        
        // Check if current user has access to household
        var currentUserMembership = await _context.UserHouseholds
            .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive);

        if (currentUserMembership == null)
        {
            return NotFound("Household not found or you don't have access to it.");
        }

        // Find the target member
        var targetMembership = await _context.UserHouseholds
            .FirstOrDefaultAsync(uh => uh.UserId == memberUserId && uh.HouseholdId == householdId && uh.IsActive);

        if (targetMembership == null)
        {
            return NotFound("Member not found in this household.");
        }

        // Check permissions
        bool canRemove = false;

        // Users can always remove themselves
        if (memberUserId == userId)
        {
            canRemove = true;
        }
        // Admins and Owners can remove Members
        else if (currentUserMembership.Role >= HouseholdRole.Admin && targetMembership.Role == HouseholdRole.Member)
        {
            canRemove = true;
        }
        // Only Owners can remove Admins
        else if (currentUserMembership.Role == HouseholdRole.Owner && targetMembership.Role == HouseholdRole.Admin)
        {
            canRemove = true;
        }

        if (!canRemove)
        {
            return Forbid("You don't have permission to remove this member.");
        }

        // Prevent removing the last owner
        if (targetMembership.Role == HouseholdRole.Owner)
        {
            var ownerCount = await _context.UserHouseholds
                .CountAsync(uh => uh.HouseholdId == householdId && uh.Role == HouseholdRole.Owner && uh.IsActive);

            if (ownerCount <= 1)
            {
                return BadRequest("Cannot remove the last owner of the household. Transfer ownership first or delete the household.");
            }
        }

        // Remove member (soft delete)
        targetMembership.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Leave household (current user removes themselves)
    /// </summary>
    [HttpPost("leave")]
    public async Task<ActionResult> LeaveHousehold(int householdId)
    {
        var userId = _currentUserService.UserId;
        return await RemoveMember(householdId, userId);
    }
}

// DTOs for member management
public class AddMemberRequest
{
    public string Email { get; set; } = string.Empty;
    public HouseholdRole? Role { get; set; } = HouseholdRole.Member;
}

public class UpdateMemberRoleRequest
{
    public HouseholdRole Role { get; set; }
}
