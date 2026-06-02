namespace Frigorino.Features.Me.Settings
{
    public sealed record UserSettingsResponse(
        string? Language,
        bool ExpiryNotificationsEnabled,
        int ExpiryLeadDays);
}
