using Frigorino.Domain.Entities;

namespace Frigorino.Domain.Interfaces;

public interface ICurrentHouseholdService
{
    /// <summary>
    /// Get the current user's active household ID (from session/context)
    /// </summary>
    Task<int?> GetCurrentHouseholdIdAsync();

    /// <summary>
    /// Set the current user's active household
    /// </summary>
    Task SetCurrentHouseholdAsync(int householdId);

    /// <summary>
    /// Get the current user's role in the active household
    /// </summary>
    Task<HouseholdRole?> GetCurrentHouseholdRoleAsync();

    /// <summary>
    /// Check if current user has access to a specific household
    /// </summary>
    Task<bool> HasHouseholdAccessAsync(int householdId);

    /// <summary>
    /// Check if current user has minimum role in active household
    /// </summary>
    Task<bool> HasMinimumRoleAsync(HouseholdRole minimumRole);

    /// <summary>
    /// Get current user's first available household (fallback)
    /// </summary>
    Task<int?> GetDefaultHouseholdIdAsync();
}
