using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Infrastructure.Tasks
{
    public class DeleteInactiveItems : IMaintenanceTask
    {
        public readonly ApplicationDbContext _dbContext;

        public DeleteInactiveItems(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            await _dbContext.Households.Where(h => !h.IsActive).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.Inventories.Where(h => !h.IsActive).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.Lists.Where(li => !li.IsActive).ExecuteDeleteAsync(cancellationToken);

            // Soft-deleted list items: purge unconditionally.
            await _dbContext.ListItems.Where(li => !li.IsActive).ExecuteDeleteAsync(cancellationToken);

            // Checked-off list items: purge past each household's retention window (default 30).
            var retention = await _dbContext.HouseholdSettings
                .ToDictionaryAsync(s => s.HouseholdId, s => s.CheckedItemRetentionDays, cancellationToken);

            var candidates = await _dbContext.ListItems
                .Where(li => li.Status)
                .Select(li => new CheckedItemCandidate(li.Id, li.List.HouseholdId, li.UpdatedAt))
                .ToListAsync(cancellationToken);

            var expiredIds = CheckedItemPurge.SelectExpiredItemIds(
                candidates, retention, DateTime.UtcNow, HouseholdSettings.DefaultCheckedItemRetentionDays);

            if (expiredIds.Count > 0)
            {
                await _dbContext.ListItems
                    .Where(li => expiredIds.Contains(li.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await _dbContext.InventoryItems.Where(h => !h.IsActive).ExecuteDeleteAsync(cancellationToken);

            await _dbContext.RecipeItems.Where(ri => !ri.IsActive).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.Recipes.Where(r => !r.IsActive).ExecuteDeleteAsync(cancellationToken);
        }
    }
}
