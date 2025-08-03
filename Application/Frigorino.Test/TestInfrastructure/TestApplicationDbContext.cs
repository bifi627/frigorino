using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.TestInfrastructure
{
    /// <summary>
    /// Test-specific ApplicationDbContext that doesn't override OnConfiguring
    /// to allow InMemory database provider to work properly
    /// </summary>
    public class TestApplicationDbContext : ApplicationDbContext
    {
        public TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
            : base(options)
        {
        }

        // Intentionally not overriding OnConfiguring to prevent PostgreSQL provider conflict
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Don't call base.OnConfiguring to avoid the UseNpgsql() call
            // Only do this for test contexts
        }
    }
}
