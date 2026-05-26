using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Frigorino.IntegrationTests.Tasks;

public class DeleteInactiveItemsTests : IAsyncLifetime
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
        // Build the context exactly like production (ApplicationDbContext.OnConfiguring calls
        // UseNpgsql() unconditionally, so we must go through AddEntityFramework, not hand-rolled options).
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
    public async Task Run_PurgesInactiveAndStaleCompleted_KeepsActiveAndRecent()
    {
        var now = DateTime.UtcNow;

        int keepHouseholdId;
        await using (var seed = _provider.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Required FK principal: Household/List.CreatedByUserId -> User.ExternalId (Restrict).
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

            var keep = new Household { Name = "keep", CreatedByUserId = "u", IsActive = true };
            var drop = new Household { Name = "drop", CreatedByUserId = "u", IsActive = false };
            db.Households.AddRange(keep, drop);
            await db.SaveChangesAsync();
            keepHouseholdId = keep.Id;

            var list = new List { Name = "list", HouseholdId = keepHouseholdId, CreatedByUserId = "u", IsActive = true };
            db.Lists.Add(list);
            await db.SaveChangesAsync();

            // Timestamps set before Add are preserved (the SaveChanges override only stamps when default).
            db.ListItems.AddRange(
                new ListItem { ListId = list.Id, Text = "inactive", IsActive = false, Status = false, CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) },
                new ListItem { ListId = list.Id, Text = "stale done", IsActive = true, Status = true, CreatedAt = now.AddDays(-40), UpdatedAt = now.AddDays(-31) },
                new ListItem { ListId = list.Id, Text = "recent done", IsActive = true, Status = true, CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-2) },
                new ListItem { ListId = list.Id, Text = "open old", IsActive = true, Status = false, CreatedAt = now.AddDays(-100), UpdatedAt = now.AddDays(-100) });
            await db.SaveChangesAsync();

            // Inactive household WITH active children — exercises the FK cascade path: deleting the
            // household must cascade to its lists/inventories/items without an FK violation (a switch
            // from Cascade to Restrict on those FKs would surface as a failing delete here).
            var dropWithChildren = new Household { Name = "drop-with-children", CreatedByUserId = "u", IsActive = false };
            db.Households.Add(dropWithChildren);
            await db.SaveChangesAsync();

            var childList = new List { Name = "child list", HouseholdId = dropWithChildren.Id, CreatedByUserId = "u", IsActive = true };
            var childInventory = new Inventory { Name = "child inventory", HouseholdId = dropWithChildren.Id, CreatedByUserId = "u", IsActive = true };
            db.Lists.Add(childList);
            db.Inventories.Add(childInventory);
            await db.SaveChangesAsync();

            db.ListItems.Add(new ListItem { ListId = childList.Id, Text = "child list item", IsActive = true, Status = false, CreatedAt = now, UpdatedAt = now });
            db.InventoryItems.Add(new InventoryItem { InventoryId = childInventory.Id, Text = "child inventory item", IsActive = true, CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
        }

        await using (var run = _provider.CreateAsyncScope())
        {
            var db = run.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await new DeleteInactiveItems(db).Run();
        }

        await using (var verify = _provider.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var households = await db.Households.Select(h => h.Name).ToListAsync();
            Assert.Equal(new[] { "keep" }, households);

            var items = await db.ListItems.OrderBy(i => i.Text).Select(i => i.Text).ToListAsync();
            Assert.Equal(new[] { "open old", "recent done" }, items);

            // The inactive household's list + inventory + their items were cascade-deleted with it.
            var lists = await db.Lists.Select(l => l.Name).ToListAsync();
            Assert.Equal(new[] { "list" }, lists);
            Assert.Empty(await db.Inventories.ToListAsync());
            Assert.Empty(await db.InventoryItems.ToListAsync());
        }
    }
}
