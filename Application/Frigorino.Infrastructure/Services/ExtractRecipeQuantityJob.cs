using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // Recipe quantity extraction. Identical to ExtractQuantityJob EXCEPT it never chains
    // classification (no IProductClassificationTrigger) — recipe items must not create Product rows.
    public class ExtractRecipeQuantityJob : IExtractRecipeQuantityJob
    {
        private readonly ApplicationDbContext _db;
        private readonly IQuantityExtractor _extractor;
        private readonly ILogger<ExtractRecipeQuantityJob> _logger;

        public ExtractRecipeQuantityJob(
            ApplicationDbContext db,
            IQuantityExtractor extractor,
            ILogger<ExtractRecipeQuantityJob> logger)
        {
            _db = db;
            _extractor = extractor;
            _logger = logger;
        }

        public async Task Run(int householdId, int recipeId, int itemId, string rawText, CancellationToken ct)
        {
            var recipe = await _db.Recipes
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            var item = recipe?.Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (recipe is null || item is null)
            {
                return;
            }

            var expectedText = rawText.Trim();
            if (!string.Equals(item.Text, expectedText, StringComparison.Ordinal))
            {
                return;
            }

            var result = await _extractor.ExtractAsync(rawText, ct);
            if (result.IsFailed)
            {
                _logger.LogWarning(
                    "Recipe quantity extraction failed for item {ItemId} (household {HouseholdId}); dropping.",
                    itemId, householdId);
                return;
            }

            var currentText = await _db.RecipeItems
                .Where(i => i.Id == itemId && i.IsActive)
                .Select(i => i.Text)
                .FirstOrDefaultAsync(ct);
            if (currentText is null || !string.Equals(currentText, expectedText, StringComparison.Ordinal))
            {
                return;
            }

            var extraction = result.Value;
            var applied = recipe.ApplyExtractedQuantity(itemId, extraction.CleanName, extraction.Quantity);
            if (applied.IsFailed)
            {
                return;
            }

            await _db.SaveChangesAsync(ct);
            // NO classification chain here — deliberate (recipe MVP). See spec Decision 1.
        }
    }
}
