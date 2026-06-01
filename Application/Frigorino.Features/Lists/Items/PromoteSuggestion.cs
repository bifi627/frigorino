using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Features.Lists.Items
{
    // Optional promote-to-inventory hint attached to the toggle response when a list item is
    // checked DONE and its product (by normalized name) is a perishable. SuggestedExpiry is a
    // date for AiRecommendsShelfLife, null for UserEntersFromPackage (user reads the package).
    public sealed record PromoteSuggestion(ExpiryHandling ExpiryHandling, DateOnly? SuggestedExpiry)
    {
        // product == null  → not yet classified / no catalog row → no suggestion.
        // non-perishable    → no suggestion.
        public static PromoteSuggestion? For(Product? product, DateOnly today)
        {
            if (product is null)
            {
                return null;
            }

            var expiry = product.EffectiveExpiry;
            if (!expiry.SuggestsInventoryTracking)
            {
                return null;
            }

            return new PromoteSuggestion(expiry.Handling, expiry.SuggestedExpiry(today));
        }
    }
}
