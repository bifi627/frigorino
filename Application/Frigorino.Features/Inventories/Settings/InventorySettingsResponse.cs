namespace Frigorino.Features.Inventories.Settings
{
    public sealed record InventorySettingsResponse(
        bool ExpiryNotificationsEnabled,
        int? ExpiryLeadDays);
}
