using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    public static class QuantityExtractionDependencyInjection
    {
        // Must be called AFTER AddItemClassification — both trigger impls depend on the
        // IProductClassificationTrigger it registers.
        public static IServiceCollection AddQuantityExtraction(
            this IServiceCollection services, IConfiguration configuration)
        {
            var enabled = configuration.GetValue<bool>("Ai:QuantityExtractor:Enabled");
            var apiKey = configuration["Ai:ApiKey"];
            var model = configuration["Ai:QuantityExtractor:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-5.4-nano";
            }

            if (enabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddKeyedSingleton<ChatClient>(AiKeys.Extractor, new ChatClient(model: model, apiKey: apiKey));
                services.AddScoped<IQuantityExtractor, OpenAiQuantityExtractor>();
                // Job depends on IQuantityExtractor — registered only on the enabled path (same
                // ValidateOnBuild reasoning as ClassifyProductJob).
                services.AddScoped<IExtractQuantityJob, ExtractQuantityJob>();
                services.AddScoped<IQuantityExtractionTrigger, QueueingQuantityExtractionTrigger>();
            }
            else
            {
                services.AddScoped<IQuantityExtractionTrigger, NullQuantityExtractionTrigger>();
            }

            return services;
        }
    }
}
