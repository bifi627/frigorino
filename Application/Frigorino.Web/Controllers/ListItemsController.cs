using Frigorino.Domain.DTOs;
using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Frigorino.Web.Controllers;

[ApiController]
[Route("api/household/{householdId}/lists/{listId}/[controller]")]
[Authorize]
public class ListItemsController : ControllerBase
{
    private readonly IListItemService _listItemService;
    private readonly ICurrentUserService _currentUserService;

    public ListItemsController(IListItemService listItemService, ICurrentUserService currentUserService)
    {
        _listItemService = listItemService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all items for a specific list (sorted by status and sort order)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ListItemDto>>> GetListItems(int householdId, int listId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var items = await _listItemService.GetItemsByListIdAsync(listId, userId);
            return Ok(items);
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
    /// Get a specific list item by ID
    /// </summary>
    [HttpGet("{itemId}")]
    public async Task<ActionResult<ListItemDto>> GetListItem(int householdId, int listId, int itemId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var item = await _listItemService.GetItemAsync(itemId, userId);

            if (item == null)
            {
                return NotFound("List item not found or you don't have access to it.");
            }

            // Ensure the item belongs to the specified list
            if (item.ListId != listId)
            {
                return BadRequest("Item does not belong to the specified list.");
            }

            return Ok(item);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Create a new item in the list (automatically goes to top of unchecked section)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ListItemDto>> CreateListItem(int householdId, int listId, CreateListItemRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var item = await _listItemService.CreateItemAsync(listId, request, userId);
            return CreatedAtAction(nameof(GetListItem), new { householdId, listId, itemId = item.Id }, item);
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
    /// Update an existing list item
    /// </summary>
    [HttpPut("{itemId}")]
    public async Task<ActionResult<ListItemDto>> UpdateListItem(int householdId, int listId, int itemId, UpdateListItemRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var item = await _listItemService.UpdateItemAsync(itemId, request, userId);

            if (item == null)
            {
                return NotFound("List item not found or you don't have access to it.");
            }

            // Ensure the item belongs to the specified list
            if (item.ListId != listId)
            {
                return BadRequest("Item does not belong to the specified list.");
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
    /// Delete a list item (soft delete)
    /// </summary>
    [HttpDelete("{itemId}")]
    public async Task<ActionResult> DeleteListItem(int householdId, int listId, int itemId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var success = await _listItemService.DeleteItemAsync(itemId, userId);

            if (!success)
            {
                return NotFound("List item not found or you don't have access to it.");
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Toggle the checked/unchecked status of an item (automatically moves to appropriate section)
    /// </summary>
    [HttpPatch("{itemId}/toggle-status")]
    public async Task<ActionResult<ListItemDto>> ToggleItemStatus(int householdId, int listId, int itemId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var item = await _listItemService.ToggleItemStatusAsync(itemId, userId);

            if (item == null)
            {
                return NotFound("List item not found or you don't have access to it.");
            }

            // Ensure the item belongs to the specified list
            if (item.ListId != listId)
            {
                return BadRequest("Item does not belong to the specified list.");
            }

            return Ok(item);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Reorder an item within its status section (checked or unchecked)
    /// </summary>
    [HttpPatch("{itemId}/reorder")]
    public async Task<ActionResult<ListItemDto>> ReorderItem(int householdId, int listId, int itemId, ReorderItemRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var item = await _listItemService.ReorderItemAsync(itemId, request, userId);

            if (item == null)
            {
                return NotFound("List item not found or you don't have access to it.");
            }

            // Ensure the item belongs to the specified list
            if (item.ListId != listId)
            {
                return BadRequest("Item does not belong to the specified list.");
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
    /// Compact sort orders for all items in the list (resets to clean gaps)
    /// </summary>
    [HttpPost("compact")]
    public async Task<ActionResult> CompactSortOrders(int householdId, int listId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var success = await _listItemService.RecalculateFullSortOrder(listId, userId);

            if (!success)
            {
                return BadRequest("Unable to compact sort orders or list is empty.");
            }

            return Ok(new { message = "Sort orders compacted successfully." });
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
