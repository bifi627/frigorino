using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Frigorino.Infrastructure.EntityFramework
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Demo> Demo { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Household> Households { get; set; }
        public DbSet<UserHousehold> UserHouseholds { get; set; }
        public DbSet<List> Lists { get; set; }
        public DbSet<ListItem> ListItems { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeItem> RecipeItems { get; set; }
        public DbSet<RecipeSection> RecipeSections { get; set; }
        public DbSet<RecipeLink> RecipeLinks { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<HouseholdSettings> HouseholdSettings { get; set; }
        public DbSet<SortBlueprint> SortBlueprints { get; set; }
        public DbSet<SortBlueprintCategory> SortBlueprintCategories { get; set; }
        public DbSet<InventorySettings> InventorySettings { get; set; }
        public DbSet<FcmToken> FcmTokens { get; set; }
        public DbSet<NotificationDispatch> NotificationDispatches { get; set; }
        public DbSet<UserInventoryNotificationSetting> UserInventoryNotificationSettings { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder.UseNpgsql();
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            base.OnModelCreating(builder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                var now = DateTime.UtcNow;

                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity is User user && user.CreatedAt == default)
                    {
                        user.CreatedAt = now;
                        user.LastLoginAt = now;
                    }

                    if (entry.Entity is Household household && household.CreatedAt == default)
                    {
                        household.CreatedAt = now;
                        household.UpdatedAt = now;
                    }

                    if (entry.Entity is UserHousehold userHousehold && userHousehold.JoinedAt == default)
                    {
                        userHousehold.JoinedAt = now;
                    }

                    if (entry.Entity is List list && list.CreatedAt == default)
                    {
                        list.CreatedAt = now;
                        list.UpdatedAt = now;
                    }

                    if (entry.Entity is ListItem listItem && listItem.CreatedAt == default)
                    {
                        listItem.CreatedAt = now;
                        listItem.UpdatedAt = now;
                    }

                    if (entry.Entity is Inventory inventory && inventory.CreatedAt == default)
                    {
                        inventory.CreatedAt = now;
                        inventory.UpdatedAt = now;
                    }

                    if (entry.Entity is InventoryItem inventoryItem && inventoryItem.CreatedAt == default)
                    {
                        inventoryItem.CreatedAt = now;
                        inventoryItem.UpdatedAt = now;
                    }

                    if (entry.Entity is Recipe recipe && recipe.CreatedAt == default)
                    {
                        recipe.CreatedAt = now;
                        recipe.UpdatedAt = now;
                    }

                    if (entry.Entity is RecipeItem recipeItem && recipeItem.CreatedAt == default)
                    {
                        recipeItem.CreatedAt = now;
                        recipeItem.UpdatedAt = now;
                    }

                    if (entry.Entity is RecipeSection recipeSection && recipeSection.CreatedAt == default)
                    {
                        recipeSection.CreatedAt = now;
                        recipeSection.UpdatedAt = now;
                    }

                    if (entry.Entity is RecipeLink recipeLink && recipeLink.CreatedAt == default)
                    {
                        recipeLink.CreatedAt = now;
                        recipeLink.UpdatedAt = now;
                    }

                    if (entry.Entity is Product product && product.CreatedAt == default)
                    {
                        product.CreatedAt = now;
                        product.UpdatedAt = now;
                    }

                    if (entry.Entity is UserSettings userSettings && userSettings.CreatedAt == default)
                    {
                        userSettings.CreatedAt = now;
                        userSettings.UpdatedAt = now;
                    }

                    if (entry.Entity is HouseholdSettings householdSettings && householdSettings.CreatedAt == default)
                    {
                        householdSettings.CreatedAt = now;
                        householdSettings.UpdatedAt = now;
                    }

                    if (entry.Entity is SortBlueprint sortBlueprintAdded && sortBlueprintAdded.CreatedAt == default)
                    {
                        sortBlueprintAdded.CreatedAt = now;
                        sortBlueprintAdded.UpdatedAt = now;
                    }

                    if (entry.Entity is InventorySettings inventorySettings && inventorySettings.CreatedAt == default)
                    {
                        inventorySettings.CreatedAt = now;
                        inventorySettings.UpdatedAt = now;
                    }

                    if (entry.Entity is FcmToken fcmTokenAdded && fcmTokenAdded.CreatedAt == default)
                    {
                        fcmTokenAdded.CreatedAt = now;
                        fcmTokenAdded.LastSeenAt = now;
                    }

                    if (entry.Entity is UserInventoryNotificationSetting uins && uins.CreatedAt == default)
                    {
                        uins.CreatedAt = now;
                        uins.UpdatedAt = now;
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (entry.Entity is Household household)
                    {
                        household.UpdatedAt = now;
                    }

                    if (entry.Entity is List list)
                    {
                        list.UpdatedAt = now;
                    }

                    if (entry.Entity is ListItem listItem)
                    {
                        listItem.UpdatedAt = now;
                    }

                    if (entry.Entity is Inventory inventory)
                    {
                        inventory.UpdatedAt = now;
                    }

                    if (entry.Entity is InventoryItem inventoryItem)
                    {
                        inventoryItem.UpdatedAt = now;
                    }

                    if (entry.Entity is Recipe recipe)
                    {
                        recipe.UpdatedAt = now;
                    }

                    if (entry.Entity is RecipeItem recipeItem)
                    {
                        recipeItem.UpdatedAt = now;
                    }

                    if (entry.Entity is RecipeSection recipeSection)
                    {
                        recipeSection.UpdatedAt = now;
                    }

                    if (entry.Entity is RecipeLink recipeLink)
                    {
                        recipeLink.UpdatedAt = now;
                    }

                    if (entry.Entity is Product product)
                    {
                        product.UpdatedAt = now;
                    }

                    if (entry.Entity is UserSettings userSettings)
                    {
                        userSettings.UpdatedAt = now;
                    }

                    if (entry.Entity is HouseholdSettings householdSettings)
                    {
                        householdSettings.UpdatedAt = now;
                    }

                    if (entry.Entity is SortBlueprint sortBlueprintModified)
                    {
                        sortBlueprintModified.UpdatedAt = now;
                    }

                    if (entry.Entity is InventorySettings inventorySettings)
                    {
                        inventorySettings.UpdatedAt = now;
                    }

                    if (entry.Entity is FcmToken fcmTokenModified)
                    {
                        fcmTokenModified.LastSeenAt = now;
                    }

                    if (entry.Entity is UserInventoryNotificationSetting uinsModified)
                    {
                        uinsModified.UpdatedAt = now;
                    }
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
