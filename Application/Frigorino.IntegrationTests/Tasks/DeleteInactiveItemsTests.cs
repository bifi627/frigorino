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
            // Distinct Rank per active item in a section: the partial unique index
            // (ListId, Status, Rank WHERE IsActive) rejects two active items sharing the default "".
            db.ListItems.AddRange(
                new ListItem { ListId = list.Id, Text = "inactive", IsActive = false, Status = false, CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) },
                new ListItem { ListId = list.Id, Text = "stale done", IsActive = true, Status = true, Rank = "a0", CreatedAt = now.AddDays(-40), UpdatedAt = now.AddDays(-31) },
                new ListItem { ListId = list.Id, Text = "recent done", IsActive = true, Status = true, Rank = "a1", CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-2) },
                new ListItem { ListId = list.Id, Text = "open old", IsActive = true, Status = false, Rank = "a0", CreatedAt = now.AddDays(-100), UpdatedAt = now.AddDays(-100) });
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

            // Soft-deleted children UNDER the surviving "keep" household — these exercise each purge's
            // `Where(!IsActive)` predicate DIRECTLY (not via the household cascade above). A regression
            // that broke a predicate on a live household — wrong filter, or hard-deleting active rows —
            // would only surface here, since every other inventory/list/recipe row gets cascade-deleted
            // with its household regardless.
            db.Lists.Add(new List { Name = "soft-deleted list", HouseholdId = keepHouseholdId, CreatedByUserId = "u", IsActive = false });

            // Active inventory under keep, holding one active item (survives) + one soft-deleted (purged).
            var keepInventory = new Inventory { Name = "keep inventory", HouseholdId = keepHouseholdId, CreatedByUserId = "u", IsActive = true };
            var dropInventory = new Inventory { Name = "soft-deleted inventory", HouseholdId = keepHouseholdId, CreatedByUserId = "u", IsActive = false };
            db.Inventories.AddRange(keepInventory, dropInventory);
            await db.SaveChangesAsync();

            db.InventoryItems.AddRange(
                new InventoryItem { InventoryId = keepInventory.Id, Text = "keep inventory item", IsActive = true, Rank = "a0", CreatedAt = now, UpdatedAt = now },
                new InventoryItem { InventoryId = keepInventory.Id, Text = "soft-deleted inventory item", IsActive = false, Rank = "a1", CreatedAt = now, UpdatedAt = now });

            // Active recipe (survives) with one active item + one soft-deleted item (purged), plus a
            // soft-deleted recipe (purged). Recipes were added after this test last changed, so the
            // recipe/recipe-item purges (DeleteInactiveItems lines 47-48) were entirely uncovered.
            var keepRecipe = new Recipe { Name = "keep recipe", HouseholdId = keepHouseholdId, CreatedByUserId = "u", IsActive = true, CreatedAt = now, UpdatedAt = now };
            var dropRecipe = new Recipe { Name = "soft-deleted recipe", HouseholdId = keepHouseholdId, CreatedByUserId = "u", IsActive = false, CreatedAt = now, UpdatedAt = now };
            db.Recipes.AddRange(keepRecipe, dropRecipe);
            await db.SaveChangesAsync();

            // Items live in a section (required FK). One active section holds them; one soft-deleted
            // (empty) section under the surviving recipe exercises the direct RecipeSections.Where(!IsActive)
            // purge — the only path that hits it, since a dropped recipe's sections go via cascade.
            var keepSection = new RecipeSection { RecipeId = keepRecipe.Id, Rank = "a0", IsActive = true, CreatedAt = now, UpdatedAt = now };
            var dropSection = new RecipeSection { RecipeId = keepRecipe.Id, Rank = "a1", IsActive = false, CreatedAt = now, UpdatedAt = now };
            db.RecipeSections.AddRange(keepSection, dropSection);

            // A link feeds the direct RecipeLinks.Where(!IsActive) purge: the active one survives,
            // the soft-deleted one is removed.
            var keepLink = new RecipeLink { RecipeId = keepRecipe.Id, Url = "https://keep.example.com", Rank = "a0", IsActive = true, CreatedAt = now, UpdatedAt = now };
            var dropLink = new RecipeLink { RecipeId = keepRecipe.Id, Url = "https://drop.example.com", Rank = "a1", IsActive = false, CreatedAt = now, UpdatedAt = now };
            db.RecipeLinks.AddRange(keepLink, dropLink);
            await db.SaveChangesAsync();

            db.RecipeItems.AddRange(
                new RecipeItem { RecipeId = keepRecipe.Id, SectionId = keepSection.Id, Text = "keep recipe item", IsActive = true, Rank = "a0", CreatedAt = now, UpdatedAt = now },
                new RecipeItem { RecipeId = keepRecipe.Id, SectionId = keepSection.Id, Text = "soft-deleted recipe item", IsActive = false, Rank = "a1", CreatedAt = now, UpdatedAt = now });
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

            // Only the active list under "keep" survives: the dropped household's "child list" went via
            // cascade, the "soft-deleted list" under keep via the direct Lists.Where(!IsActive) purge.
            var lists = await db.Lists.Select(l => l.Name).ToListAsync();
            Assert.Equal(new[] { "list" }, lists);

            // Direct (non-cascade) soft-delete purges on the surviving household: active rows survive,
            // soft-deleted rows are gone. The dropped household's inventory/items went via cascade.
            var inventories = await db.Inventories.OrderBy(i => i.Name).Select(i => i.Name).ToListAsync();
            Assert.Equal(new[] { "keep inventory" }, inventories);
            var inventoryItems = await db.InventoryItems.Select(i => i.Text).ToListAsync();
            Assert.Equal(new[] { "keep inventory item" }, inventoryItems);

            var recipes = await db.Recipes.Select(r => r.Name).ToListAsync();
            Assert.Equal(new[] { "keep recipe" }, recipes);
            var recipeItems = await db.RecipeItems.Select(r => r.Text).ToListAsync();
            Assert.Equal(new[] { "keep recipe item" }, recipeItems);

            // The soft-deleted section under the surviving recipe is gone (direct purge); the active
            // one survives.
            var recipeSections = await db.RecipeSections.CountAsync();
            Assert.Equal(1, recipeSections);

            // The soft-deleted link under the surviving recipe is gone (direct purge); the active
            // one survives.
            var recipeLinks = await db.RecipeLinks.CountAsync();
            Assert.Equal(1, recipeLinks);
        }
    }
}
