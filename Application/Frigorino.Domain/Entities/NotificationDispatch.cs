namespace Frigorino.Domain.Entities
{
    // De-dup ledger: at most one notification per (user, inventory, day). A unique index on
    // (UserId, InventoryId, SentOn) makes the scan idempotent across re-triggers / double fires.
    public class NotificationDispatch
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int InventoryId { get; set; }
        public DateOnly SentOn { get; set; }

        public static NotificationDispatch Create(string userId, int inventoryId, DateOnly sentOn)
        {
            return new NotificationDispatch
            {
                UserId = userId,
                InventoryId = inventoryId,
                SentOn = sentOn,
            };
        }
    }
}
