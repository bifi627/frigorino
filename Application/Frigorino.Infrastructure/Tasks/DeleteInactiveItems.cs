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

            var thresholdDate = DateTime.UtcNow.AddDays(-30);
            await _dbContext.ListItems.Where(li => !li.IsActive || (li.Status && li.UpdatedAt < thresholdDate)).ExecuteDeleteAsync(cancellationToken);

            await _dbContext.InventoryItems.Where(h => !h.IsActive).ExecuteDeleteAsync(cancellationToken);
        }
    }
}
