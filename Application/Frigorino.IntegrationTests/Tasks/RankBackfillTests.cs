using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Frigorino.IntegrationTests.Tasks;

// Exercises the one-time expand-phase backfill against a real Postgres (the C collation + IS NULL
// behavior is provider-specific — InMemory can't reproduce it). Pre-migration rows are simulated by
// NULLing their Rank via raw SQL, since the EF model maps Rank required and won't insert NULL.
public class RankBackfillTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _container.GetConnectionString(),
            })
            .Build();
        services.AddEntityFramework(configuration);
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task RunAsync_FillsNullRanks_InLegacySortOrderOrder()
    {
        var now = DateTime.UtcNow;

        await using (var seed = _provider.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.Users.Add(new User
            {
                ExternalId = "u",
                Name = "Seed User",
                Email = "seed@frigorino.local",
                CreatedAt = now,
                LastLoginAt = now,
                IsActive = true,
            });
            await db.SaveChangesAsync();

            var household = new Household { Name = "h", CreatedByUserId = "u", IsActive = true };
            db.Households.Add(household);
            await db.SaveChangesAsync();

            var list = new List { Name = "list", HouseholdId = household.Id, CreatedByUserId = "u", IsActive = true };
            var inventory = new Inventory { Name = "inv", HouseholdId = household.Id, CreatedByUserId = "u", IsActive = true };
            db.Lists.Add(list);
            db.Inventories.Add(inventory);
            await db.SaveChangesAsync();

            // SortOrder is deliberately out of insertion order so the assertion proves the backfill
            // derives rank order from SortOrder, not from Id/insertion order. Distinct placeholder
            // ranks are needed only to clear the partial unique index on insert — they're NULLed
            // immediately below to simulate genuine pre-migration rows.
            db.ListItems.AddRange(
                new ListItem { ListId = list.Id, Text = "A", SortOrder = 30, Rank = "a0", Status = false, IsActive = true, CreatedAt = now, UpdatedAt = now },
                new ListItem { ListId = list.Id, Text = "B", SortOrder = 10, Rank = "a1", Status = false, IsActive = true, CreatedAt = now, UpdatedAt = now },
                new ListItem { ListId = list.Id, Text = "C", SortOrder = 20, Rank = "a2", Status = false, IsActive = true, CreatedAt = now, UpdatedAt = now });
            db.InventoryItems.AddRange(
                new InventoryItem { InventoryId = inventory.Id, Text = "X", SortOrder = 200, Rank = "a0", IsActive = true, CreatedAt = now, UpdatedAt = now },
                new InventoryItem { InventoryId = inventory.Id, Text = "Y", SortOrder = 100, Rank = "a1", IsActive = true, CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();

            // Simulate pre-migration rows: NULL out the placeholder ranks so the backfill has work to do.
            await db.Database.ExecuteSqlRawAsync("UPDATE \"ListItems\" SET \"Rank\" = NULL");
            await db.Database.ExecuteSqlRawAsync("UPDATE \"InventoryItems\" SET \"Rank\" = NULL");
        }

        await using (var run = _provider.CreateAsyncScope())
        {
            var db = run.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await RankBackfill.RunAsync(db, NullLogger.Instance);
        }

        await using (var verify = _provider.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var listItems = await db.ListItems.AsNoTracking().ToListAsync();
            Assert.All(listItems, i => Assert.False(string.IsNullOrEmpty(i.Rank), $"list item '{i.Text}' has empty rank"));
            // Ordered by rank, items must appear in SortOrder order: B(10), C(20), A(30).
            var listByRank = listItems.OrderBy(i => i.Rank, StringComparer.Ordinal).Select(i => i.Text).ToArray();
            Assert.Equal(new[] { "B", "C", "A" }, listByRank);

            var invItems = await db.InventoryItems.AsNoTracking().ToListAsync();
            Assert.All(invItems, i => Assert.False(string.IsNullOrEmpty(i.Rank), $"inventory item '{i.Text}' has empty rank"));
            // Ordered by rank: Y(100), X(200).
            var invByRank = invItems.OrderBy(i => i.Rank, StringComparer.Ordinal).Select(i => i.Text).ToArray();
            Assert.Equal(new[] { "Y", "X" }, invByRank);
        }
    }
}
