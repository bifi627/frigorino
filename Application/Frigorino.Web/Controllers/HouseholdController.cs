using Frigorino.Domain.DTOs;
using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Frigorino.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HouseholdController : ControllerBase
{
    private readonly IHouseholdService _householdService;
    private readonly ICurrentUserService _currentUserService;

    public HouseholdController(IHouseholdService householdService, ICurrentUserService currentUserService)
    {
        _householdService = householdService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all households the current user belongs to
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<HouseholdDto>>> GetUserHouseholds()
    {
        var userId = _currentUserService.UserId;
        var households = await _householdService.GetUserHouseholdsAsync(userId);
        return Ok(households);
    }

    /// <summary>
    /// Get a specific household by ID (if user has access)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<HouseholdDto>> GetHousehold(int id)
    {
        var userId = _currentUserService.UserId;
        var household = await _householdService.GetHouseholdAsync(id, userId);
        
        if (household == null)
        {
            return NotFound("Household not found or you don't have access to it.");
        }

        return Ok(household);
    }

    /// <summary>
    /// Create a new household
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<HouseholdDto>> CreateHousehold(CreateHouseholdRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var household = await _householdService.CreateHouseholdAsync(request, userId);
            return Ok(household);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update a household (Admin/Owner only)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<HouseholdDto>> UpdateHousehold(int id, UpdateHouseholdRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var household = await _householdService.UpdateHouseholdAsync(id, request, userId);
            
            if (household == null)
            {
                return NotFound("Household not found or you don't have access to it.");
            }

            return Ok(household);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Delete a household (Owner only)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteHousehold(int id)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var result = await _householdService.DeleteHouseholdAsync(id, userId);
            
            if (!result)
            {
                return NotFound("Household not found or you don't have access to it.");
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }
}
