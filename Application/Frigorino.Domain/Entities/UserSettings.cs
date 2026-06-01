using FluentResults;

namespace Frigorino.Domain.Entities
{
    public class UserSettings
    {
        // Languages with a translation bundle under ClientApp/public/locales. Single source
        // of truth for both SetLanguage validation and the read-side default.
        public static readonly string[] SupportedLanguages = ["en", "de"];

        public string UserId { get; set; } = string.Empty;

        // null = no explicit choice; the client falls back to browser language detection.
        public string? Language { get; set; }

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
    }
}
