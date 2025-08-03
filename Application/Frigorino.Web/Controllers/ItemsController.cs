using Frigorino.Domain.DTOs;
using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Frigorino.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ItemsController : ControllerBase
{
    private readonly IListItemService _listItemService;
    private readonly ICurrentUserService _currentUserService;

    public ItemsController(IListItemService listItemService, ICurrentUserService currentUserService)
    {
        _listItemService = listItemService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get a specific item by ID (direct access)
    /// </summary>
    [HttpGet("{itemId}")]
    public async Task<ActionResult<ListItemDto>> GetItem(int itemId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var item = await _listItemService.GetItemAsync(itemId, userId);

            if (item == null)
            {
                return NotFound("Item not found or you don't have access to it.");
            }

            return Ok(item);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Update an item (direct access)
    /// </summary>
    [HttpPut("{itemId}")]
    public async Task<ActionResult<ListItemDto>> UpdateItem(int itemId, UpdateListItemRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var item = await _listItemService.UpdateItemAsync(itemId, request, userId);

            if (item == null)
            {
                return NotFound("Item not found or you don't have access to it.");
            }

            return Ok(item);
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
    /// Delete an item (direct access)
    /// </summary>
    [HttpDelete("{itemId}")]
    public async Task<ActionResult> DeleteItem(int itemId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var success = await _listItemService.DeleteItemAsync(itemId, userId);

            if (!success)
            {
                return NotFound("Item not found or you don't have access to it.");
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Toggle item status (direct access)
    /// </summary>
    [HttpPatch("{itemId}/toggle-status")]
    public async Task<ActionResult<ListItemDto>> ToggleStatus(int itemId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var item = await _listItemService.ToggleItemStatusAsync(itemId, userId);

            if (item == null)
            {
                return NotFound("Item not found or you don't have access to it.");
            }

            return Ok(item);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Reorder an item (direct access)
    /// </summary>
    [HttpPatch("{itemId}/reorder")]
    public async Task<ActionResult<ListItemDto>> ReorderItem(int itemId, ReorderItemRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var item = await _listItemService.ReorderItemAsync(itemId, request, userId);

            if (item == null)
            {
                return NotFound("Item not found or you don't have access to it.");
            }

            return Ok(item);
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
}
