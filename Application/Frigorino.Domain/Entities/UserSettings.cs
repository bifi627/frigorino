using FluentResults;

namespace Frigorino.Domain.Entities
{
    public class UserSettings
    {
        // Languages with a translation bundle under ClientApp/public/locales. Single source
        // of truth for both SetLanguage validation and the read-side default.
        public static readonly string[] SupportedLanguages = ["en", "de"];

        // Lead-time bounds + default for expiry notifications. The default applies to brand-new
        // settings rows and to users who never opened notification settings.
        public const int DefaultExpiryLeadDays = 3;
        public const int MinExpiryLeadDays = 0;
        public const int MaxExpiryLeadDays = 365;

        public string UserId { get; set; } = string.Empty;

        // null = no explicit choice; the client falls back to browser language detection.
        public string? Language { get; set; }

        // Global opt-in. Default false: the user must explicitly enable (which also drives the
        // browser push-permission grant on the client).
        public bool ExpiryNotificationsEnabled { get; set; }

        // Fallback lead window when an inventory does not override it.
        public int ExpiryLeadDays { get; set; } = DefaultExpiryLeadDays;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public User User { get; set; } = null!;

        public static UserSettings Create(string userId)
        {
            return new UserSettings { UserId = userId };
        }

        // The "Property" metadata key duplicates Frigorino.Features.Results.ResultExtensions.PropertyMetadataKey
        // by convention — Domain stays free of a Features dependency.
        public Result SetLanguage(string? language)
        {
            if (language is not null && !SupportedLanguages.Contains(language))
            {
                return Result.Fail(new Error($"Language must be one of: {string.Join(", ", SupportedLanguages)}.")
                    .WithMetadata("Property", nameof(Language)));
            }

            Language = language;
            return Result.Ok();
        }

        public Result SetExpiryNotifications(bool enabled, int leadDays)
        {
            if (leadDays < MinExpiryLeadDays || leadDays > MaxExpiryLeadDays)
            {
                return Result.Fail(new Error($"Lead time must be between {MinExpiryLeadDays} and {MaxExpiryLeadDays} days.")
                    .WithMetadata("Property", nameof(ExpiryLeadDays)));
            }

            ExpiryNotificationsEnabled = enabled;
            ExpiryLeadDays = leadDays;
            return Result.Ok();
        }
    }
}
