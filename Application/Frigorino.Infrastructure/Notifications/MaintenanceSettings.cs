namespace Frigorino.Infrastructure.Notifications
{
    public class MaintenanceSettings
    {
        public const string SECTION_NAME = "MaintenanceSettings";

        // Shared secret the external scheduler sends in the X-Maintenance-Key header.
        // Empty ⇒ the scan endpoint rejects everything (returns 404).
        public string TriggerToken { get; set; } = "";

        // How many days past expiry an item still appears in the digest. Upcoming + recently-expired
        // items notify; an item more than this many days overdue drops off (so a permanently-overdue
        // item is not re-listed in every daily digest forever).
        public int OverdueGraceDays { get; set; } = 1;

        // Blobs younger than this are never reclaimed by the orphan sweep — protects an in-flight
        // upload whose ListItems row is not yet committed.
        public int OrphanBlobGraceHours { get; set; } = 24;
    }
}
