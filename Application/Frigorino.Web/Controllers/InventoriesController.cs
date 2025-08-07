using Frigorino.Domain.DTOs;
using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Frigorino.Web.Controllers;

[ApiController]
[Route("api/household/{householdId}/[controller]")]
[Authorize]
public class InventoriesController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ICurrentUserService _currentUserService;

    public InventoriesController(IInventoryService inventoryService, ICurrentUserService currentUserService)
    {
        _inventoryService = inventoryService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all inventories for a specific household
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryDto>>> GetHouseholdInventories(int householdId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var inventories = await _inventoryService.GetAllInventories(householdId, userId);
            return Ok(inventories);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Get a specific inventory by ID
    /// </summary>
    [HttpGet("{inventoryId}")]
    public async Task<ActionResult<InventoryDto>> GetInventory(int householdId, int inventoryId)
    {
        var userId = _currentUserService.UserId;
        var inventory = await _inventoryService.GetInventoryAsync(inventoryId, userId);

        if (inventory == null)
        {
            return NotFound("Inventory not found or you don't have access to it.");
        }

        // Ensure the inventory belongs to the specified household
        if (inventory.HouseholdId != householdId)
        {
            return BadRequest("Inventory does not belong to the specified household.");
        }

        return Ok(inventory);
    }

    /// <summary>
    /// Create a new inventory in the household
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InventoryDto>> CreateInventory(int householdId, CreateInventoryRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var inventory = await _inventoryService.CreateInventoryAsync(householdId, request, userId);
            return Ok(inventory);
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
    /// Update an existing inventory (Creator/Admin/Owner only)
    /// </summary>
    [HttpPut("{inventoryId}")]
    public async Task<ActionResult<InventoryDto>> UpdateInventory(int householdId, int inventoryId, UpdateInventoryRequest request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var inventory = await _inventoryService.UpdateInventoryAsync(inventoryId, request, userId);

            if (inventory == null)
            {
                return NotFound("Inventory not found or you don't have access to it.");
            }

            // Ensure the inventory belongs to the specified household
            if (inventory.HouseholdId != householdId)
            {
                return BadRequest("Inventory does not belong to the specified household.");
            }

            return Ok(inventory);
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
    /// Delete an inventory (Creator/Admin/Owner only)
    /// </summary>
    [HttpDelete("{inventoryId}")]
    public async Task<ActionResult> DeleteInventory(int householdId, int inventoryId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var success = await _inventoryService.DeleteInventoryAsync(inventoryId, userId);

            if (!success)
            {
                return NotFound("Inventory not found or you don't have access to it.");
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }
}
