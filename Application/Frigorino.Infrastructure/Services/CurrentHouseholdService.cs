using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;

namespace Frigorino.Infrastructure.Services;

public class CurrentHouseholdService : ICurrentHouseholdService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Session key for storing current household ID
    private const string CurrentHouseholdSessionKey = "CurrentHouseholdId";

    public CurrentHouseholdService(
        ApplicationDbContext context, 
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<int?> GetCurrentHouseholdIdAsync()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null) return null;

        // Try to get from session
        if (session.TryGetValue(CurrentHouseholdSessionKey, out var householdIdBytes))
        {
            var householdId = BitConverter.ToInt32(householdIdBytes);
            
            // Verify user still has access to this household
            if (await HasHouseholdAccessAsync(householdId))
            {
                return householdId;
            }
            else
            {
                // Remove invalid household from session
                session.Remove(CurrentHouseholdSessionKey);
            }
        }

        // Fallback to user's first available household
        var defaultHouseholdId = await GetDefaultHouseholdIdAsync();
        if (defaultHouseholdId.HasValue)
        {
            await SetCurrentHouseholdAsync(defaultHouseholdId.Value);
            return defaultHouseholdId;
        }

        return null;
    }

    public async Task SetCurrentHouseholdAsync(int householdId)
    {
        // Verify user has access to this household
        if (!await HasHouseholdAccessAsync(householdId))
        {
            throw new UnauthorizedAccessException("You don't have access to this household.");
        }

        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.Set(CurrentHouseholdSessionKey, BitConverter.GetBytes(householdId));
        }
    }

    public async Task<HouseholdRole?> GetCurrentHouseholdRoleAsync()
    {
        var householdId = await GetCurrentHouseholdIdAsync();
        if (!householdId.HasValue) return null;

        var userId = _currentUserService.UserId;
        
        var role = await _context.UserHouseholds
            .Where(uh => uh.UserId == userId && uh.HouseholdId == householdId.Value && uh.IsActive)
            .Select(uh => uh.Role)
            .FirstOrDefaultAsync();

        return role;
    }

    public async Task<bool> HasHouseholdAccessAsync(int householdId)
    {
        var userId = _currentUserService.UserId;
        
        return await _context.UserHouseholds
            .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive);
    }

    public async Task<bool> HasMinimumRoleAsync(HouseholdRole minimumRole)
    {
        var currentRole = await GetCurrentHouseholdRoleAsync();
        if (!currentRole.HasValue) return false;

        return currentRole.Value >= minimumRole;
    }

    public async Task<int?> GetDefaultHouseholdIdAsync()
    {
        var userId = _currentUserService.UserId;
        
        // Get user's first household (ordered by highest role, then by join date)
        var householdId = await _context.UserHouseholds
            .Where(uh => uh.UserId == userId && uh.IsActive)
            .Include(uh => uh.Household)
            .Where(uh => uh.Household.IsActive)
            .OrderByDescending(uh => uh.Role)
            .ThenBy(uh => uh.JoinedAt)
            .Select(uh => uh.HouseholdId)
            .FirstOrDefaultAsync();

        return householdId == 0 ? null : householdId;
    }
}
