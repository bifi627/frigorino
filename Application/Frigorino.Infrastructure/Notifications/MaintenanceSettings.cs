namespace Frigorino.Infrastructure.Notifications
{
    public class MaintenanceSettings
    {
        public const string SECTION_NAME = "MaintenanceSettings";

        // Shared secret the external scheduler sends in the X-Maintenance-Key header.
        // Empty ⇒ the scan endpoint rejects everything (returns 404).
        public string TriggerToken { get; set; } = "";
    }
}
