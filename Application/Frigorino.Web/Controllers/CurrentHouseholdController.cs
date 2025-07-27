using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Frigorino.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CurrentHouseholdController : ControllerBase
{
    private readonly ICurrentHouseholdService _currentHouseholdService;

    public CurrentHouseholdController(ICurrentHouseholdService currentHouseholdService)
    {
        _currentHouseholdService = currentHouseholdService;
    }

    /// <summary>
    /// Get the current active household ID
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CurrentHouseholdResponse>> GetCurrentHousehold()
    {
        var householdId = await _currentHouseholdService.GetCurrentHouseholdIdAsync();
        var role = await _currentHouseholdService.GetCurrentHouseholdRoleAsync();

        if (!householdId.HasValue)
        {
            return NotFound();
        }

        return Ok(new CurrentHouseholdResponse
        {
            HouseholdId = householdId.Value,
            Role = role,
            HasActiveHousehold = true
        });
    }

    /// <summary>
    /// Set the current active household
    /// </summary>
    [HttpPost("{householdId}")]
    public async Task<ActionResult<CurrentHouseholdResponse>> SetCurrentHousehold(int householdId)
    {
        try
        {
            await _currentHouseholdService.SetCurrentHouseholdAsync(householdId);

            var role = await _currentHouseholdService.GetCurrentHouseholdRoleAsync();

            return Ok(new CurrentHouseholdResponse
            {
                HouseholdId = householdId,
                Role = role,
                HasActiveHousehold = true
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("You don't have access to this household.");
        }
    }
}

public class CurrentHouseholdResponse
{
    public int? HouseholdId { get; set; }
    public HouseholdRole? Role { get; set; }
    public bool HasActiveHousehold { get; set; }
}
