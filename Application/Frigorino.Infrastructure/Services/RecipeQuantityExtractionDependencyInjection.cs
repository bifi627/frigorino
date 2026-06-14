using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class RecipeQuantityExtractionDependencyInjection
    {
        // Call AFTER AddQuantityExtraction — reuses the IQuantityExtractor it registers on the
        // enabled path. Gated on the same Ai:QuantityExtractor:Enabled + Ai:ApiKey flags.
        public static IServiceCollection AddRecipeQuantityExtraction(
            this IServiceCollection services, IConfiguration configuration)
        {
            var enabled = configuration.GetValue<bool>("Ai:QuantityExtractor:Enabled");
            var apiKey = configuration["Ai:ApiKey"];

            if (enabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddScoped<IExtractRecipeQuantityJob, ExtractRecipeQuantityJob>();
                services.AddScoped<IRecipeQuantityExtractionTrigger, QueueingRecipeQuantityExtractionTrigger>();
            }
            else
            {
                services.AddScoped<IRecipeQuantityExtractionTrigger, NullRecipeQuantityExtractionTrigger>();
            }

            return services;
        }
    }
}
