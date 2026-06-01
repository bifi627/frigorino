namespace Frigorino.Domain.Entities
{
    // De-dup ledger: at most one digest per (user, household, day). A unique index on
    // (UserId, HouseholdId, SentOn) makes the scan idempotent across re-triggers / double fires.
    public class NotificationDispatch
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int HouseholdId { get; set; }
        public DateOnly SentOn { get; set; }

        public static NotificationDispatch Create(string userId, int householdId, DateOnly sentOn)
        {
            return new NotificationDispatch
            {
                UserId = userId,
                HouseholdId = householdId,
                SentOn = sentOn,
            };
        }
    }
}
