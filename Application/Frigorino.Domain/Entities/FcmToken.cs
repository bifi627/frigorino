namespace Frigorino.Domain.Entities
{
    // One row per device/browser registration. A user has many. Globally unique by Token
    // (a registration string identifies one device); re-registering reassigns it to the
    // current user. Timestamps are stamped centrally in ApplicationDbContext.SaveChangesAsync.
    public class FcmToken
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastSeenAt { get; set; }

        // Navigation property
        public User User { get; set; } = null!;

        public static FcmToken Create(string userId, string token)
        {
            return new FcmToken { UserId = userId, Token = token };
        }
    }
}
