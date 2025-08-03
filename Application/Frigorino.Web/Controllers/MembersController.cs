using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Frigorino.Domain.DTOs;
using Frigorino.Domain.Interfaces;

namespace Frigorino.Web.Controllers;

[ApiController]
[Route("api/household/{householdId}/[controller]")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly IHouseholdService _householdService;
    private readonly ICurrentUserService _currentUserService;

    public MembersController(IHouseholdService householdService, ICurrentUserService currentUserService)
    {
        _householdService = householdService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all members of a household
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<HouseholdMemberDto>>> GetHouseholdMembers(int householdId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var members = await _householdService.GetHouseholdMembersAsync(householdId, userId);
            return Ok(members);
        }
        catch (UnauthorizedAccessException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Add a user to household by email (Admin/Owner only)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<HouseholdMemberDto>> AddMember(int householdId, AddMemberRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var member = await _householdService.AddMemberAsync(householdId, request, userId);
            return Ok(member);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update a member's role (Admin/Owner only)
    /// </summary>
    [HttpPut("{targetUserId}/role")]
    public async Task<ActionResult<HouseholdMemberDto>> UpdateMemberRole(int householdId, string targetUserId, UpdateMemberRoleRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var member = await _householdService.UpdateMemberRoleAsync(householdId, targetUserId, request, userId);
            
            if (member == null)
            {
                return NotFound("Member not found in this household.");
            }

            return Ok(member);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Remove a member from household (Admin/Owner only, or user can remove themselves)
    /// </summary>
    [HttpDelete("{targetUserId}")]
    public async Task<ActionResult> RemoveMember(int householdId, string targetUserId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var result = await _householdService.RemoveMemberAsync(householdId, targetUserId, userId);
            
            if (!result)
            {
                return NotFound("Member not found in this household.");
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Leave household (current user removes themselves)
    /// </summary>
    [HttpPost("leave")]
    public async Task<ActionResult> LeaveHousehold(int householdId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var result = await _householdService.LeaveHouseholdAsync(householdId, userId);
            
            if (!result)
            {
                return NotFound("You are not a member of this household.");
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
