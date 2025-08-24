using Frigorino.Domain.DTOs;
using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Frigorino.Web.Controllers;

[ApiController]
[Route("api/inventory/{inventoryId}/[controller]")]
[Authorize]
public class InventoryItemsController : ControllerBase
{
    private readonly IInventoryItemService _inventoryItemService;
    private readonly ICurrentUserService _currentUserService;

    public InventoryItemsController(IInventoryItemService inventoryItemService, ICurrentUserService currentUserService)
    {
        _inventoryItemService = inventoryItemService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all inventory items for a specific inventory
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetInventoryItems(int inventoryId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var inventoryItems = await _inventoryItemService.GetAllInventoryItems(inventoryId, userId);
            return Ok(inventoryItems);
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
    /// Get a specific inventory item by ID
    /// </summary>
    [HttpGet("{inventoryItemId}")]
    public async Task<ActionResult<InventoryItemDto>> GetInventoryItem(int inventoryId, int inventoryItemId)
    {
        var userId = _currentUserService.UserId;
        var inventoryItem = await _inventoryItemService.GetInventoryItemAsync(inventoryItemId, userId);

        if (inventoryItem == null)
        {
            return NotFound("Inventory item not found or you don't have access to it.");
        }

        // Ensure the inventory item belongs to the specified inventory
        if (inventoryItem.InventoryId != inventoryId)
        {
            return BadRequest("Inventory item does not belong to the specified inventory.");
        }

        return Ok(inventoryItem);
    }

    /// <summary>
    /// Create a new inventory item in the inventory
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InventoryItemDto>> CreateInventoryItem(int inventoryId, CreateInventoryItemRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var inventoryItem = await _inventoryItemService.CreateInventoryItemAsync(inventoryId, request, userId);
            return Ok(inventoryItem);
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
    /// Update an existing inventory item (Creator/Admin/Owner only)
    /// </summary>
    [HttpPut("{inventoryItemId}")]
    public async Task<ActionResult<InventoryItemDto>> UpdateInventoryItem(int inventoryId, int inventoryItemId, UpdateInventoryItemRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var inventoryItem = await _inventoryItemService.UpdateInventoryItemAsync(inventoryItemId, request, userId);

            if (inventoryItem == null)
            {
                return NotFound("Inventory item not found or you don't have access to it.");
            }

            // Ensure the inventory item belongs to the specified inventory
            if (inventoryItem.InventoryId != inventoryId)
            {
                return BadRequest("Inventory item does not belong to the specified inventory.");
            }

            return Ok(inventoryItem);
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
    /// Delete an inventory item (Creator/Admin/Owner only)
    /// </summary>
    [HttpDelete("{inventoryItemId}")]
    public async Task<ActionResult> DeleteInventoryItem(int inventoryId, int inventoryItemId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var success = await _inventoryItemService.DeleteInventoryItemAsync(inventoryItemId, userId);

            if (!success)
            {
                return NotFound("Inventory item not found or you don't have access to it.");
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Reorder an item within its status section (checked or unchecked)
    /// </summary>
    [HttpPatch("{inventoryItemId}/reorder")]
    public async Task<ActionResult<InventoryItemDto>> ReorderItem(int householdId, int inventoryId, int inventoryItemId, ReorderItemRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var item = await _inventoryItemService.ReorderItemAsync(inventoryItemId, request, userId);

            if (item == null)
            {
                return NotFound("List item not found or you don't have access to it.");
            }

            // Ensure the item belongs to the specified list
            if (item.InventoryId != inventoryId)
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
}
