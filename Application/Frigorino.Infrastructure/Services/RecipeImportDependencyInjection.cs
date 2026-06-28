using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class RecipeImportDependencyInjection
    {
        // No config gate, no Null impl: the deterministic JSON-LD path has no vendor/API key — always on.
        public static IServiceCollection AddRecipeImport(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddSingleton(sp => new RecipeImportService(
                RecipeImportService.BuildGuardedClient(),
                sp.GetRequiredService<IMemoryCache>()));
            return services;
        }
    }
}
