using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    public static class ItemClassificationDependencyInjection
    {
        public static IServiceCollection AddItemClassification(
            this IServiceCollection services, IConfiguration configuration)
        {
            var enabled = configuration.GetValue<bool>("Ai:Classifier:Enabled");
            var apiKey = configuration["Ai:ApiKey"];
            var model = configuration["Ai:Classifier:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-5.4-mini";
            }

            if (enabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddKeyedSingleton<ChatClient>(AiKeys.Classifier, new ChatClient(model: model, apiKey: apiKey));
                services.AddScoped<IItemClassifier, OpenAiItemClassifier>();
                // The job depends on IItemClassifier, so it is registered only on the enabled path —
                // and only the enabled (queueing) trigger ever enqueues it. Registering it
                // unconditionally would fail ValidateOnBuild when no classifier is wired.
                services.AddScoped<IClassifyProductJob, ClassifyProductJob>();
                services.AddScoped<IProductClassificationTrigger, QueueingProductClassificationTrigger>();
            }
            else
            {
                // No key configured: classification is a no-op (nothing enqueued, nothing written).
                services.AddScoped<IProductClassificationTrigger, NullProductClassificationTrigger>();
            }

            return services;
        }
    }
}
