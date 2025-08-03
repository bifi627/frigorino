using Frigorino.Domain.DTOs;
using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Frigorino.Web.Controllers;

[ApiController]
[Route("api/household/{householdId}/[controller]")]
[Authorize]
public class ListsController : ControllerBase
{
    private readonly IListService _listService;
    private readonly ICurrentUserService _currentUserService;

    public ListsController(IListService listService, ICurrentUserService currentUserService)
    {
        _listService = listService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all lists for a specific household
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ListDto>>> GetHouseholdLists(int householdId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var lists = await _listService.GetAllLists(householdId, userId);
            return Ok(lists);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Get a specific list by ID
    /// </summary>
    [HttpGet("{listId}")]
    public async Task<ActionResult<ListDto>> GetList(int householdId, int listId)
    {
        var userId = _currentUserService.UserId;
        var list = await _listService.GetListAsync(listId, userId);

        if (list == null)
        {
            return NotFound("List not found or you don't have access to it.");
        }

        // Ensure the list belongs to the specified household
        if (list.HouseholdId != householdId)
        {
            return BadRequest("List does not belong to the specified household.");
        }

        return Ok(list);
    }

    /// <summary>
    /// Create a new list in the household
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ListDto>> CreateList(int householdId, CreateListRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var list = await _listService.CreateListAsync(householdId, request, userId);
            return Ok(list);
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
    /// Update an existing list (Creator/Admin/Owner only)
    /// </summary>
    [HttpPut("{listId}")]
    public async Task<ActionResult<ListDto>> UpdateList(int householdId, int listId, UpdateListRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var list = await _listService.UpdateListAsync(listId, request, userId);

            if (list == null)
            {
                return NotFound("List not found or you don't have access to it.");
            }

            // Ensure the list belongs to the specified household
            if (list.HouseholdId != householdId)
            {
                return BadRequest("List does not belong to the specified household.");
            }

            return Ok(list);
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
    /// Delete a list (Creator/Admin/Owner only)
    /// </summary>
    [HttpDelete("{listId}")]
    public async Task<ActionResult> DeleteList(int householdId, int listId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var success = await _listService.DeleteListAsync(listId, userId);

            if (!success)
            {
                return NotFound("List not found or you don't have access to it.");
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }
}
