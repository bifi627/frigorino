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
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (entry.Entity is Household household)
                    {
                        household.UpdatedAt = now;
                    }
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
