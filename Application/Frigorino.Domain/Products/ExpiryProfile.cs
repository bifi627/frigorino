using FluentResults;

namespace Frigorino.Domain.Products
{
    // Pure domain value object: how a product expires. Persisted as flat columns on Product
    // (not an EF owned type), mirroring the Quantity VO approach.
    public readonly record struct ExpiryProfile
    {
        public const int ShelfLifeDaysMin = 1;
        public const int ShelfLifeDaysMax = 365;

        public ExpiryHandling Handling { get; }
        public int? ShelfLifeDays { get; }

        private ExpiryProfile(ExpiryHandling handling, int? shelfLifeDays)
        {
            Handling = handling;
            ShelfLifeDays = shelfLifeDays;
        }

        // default(ExpiryProfile) == NonPerishable with no shelf-life, which is a valid state.
        public static ExpiryProfile NonPerishable => new(ExpiryHandling.NonPerishable, null);

        // Invariant: ShelfLifeDays is set iff Handling == AiRecommendsShelfLife, range 1..365.
        public static Result<ExpiryProfile> Create(ExpiryHandling handling, int? shelfLifeDays)
        {
            if (handling == ExpiryHandling.AiRecommendsShelfLife)
            {
                if (shelfLifeDays is null)
                {
                    return Result.Fail<ExpiryProfile>(
                        new Error("Shelf-life days are required when AI recommends a shelf life.")
                            .WithMetadata("Property", nameof(ShelfLifeDays)));
                }
                if (shelfLifeDays < ShelfLifeDaysMin || shelfLifeDays > ShelfLifeDaysMax)
                {
                    return Result.Fail<ExpiryProfile>(
                        new Error($"Shelf-life days must be between {ShelfLifeDaysMin} and {ShelfLifeDaysMax}.")
                            .WithMetadata("Property", nameof(ShelfLifeDays)));
                }
            }
            else if (shelfLifeDays is not null)
            {
                return Result.Fail<ExpiryProfile>(
                    new Error("Shelf-life days are only valid when AI recommends a shelf life.")
                        .WithMetadata("Property", nameof(ShelfLifeDays)));
            }

            return Result.Ok(new ExpiryProfile(handling, shelfLifeDays));
        }

        // Only an AI-recommended shelf life yields a concrete suggested date.
        public DateOnly? SuggestedExpiry(DateOnly today)
        {
            return Handling == ExpiryHandling.AiRecommendsShelfLife
                ? today.AddDays(ShelfLifeDays!.Value)
                : null;
        }
    }
}
