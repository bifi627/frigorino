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

}
