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
            var enabled = configuration.GetValue<bool>("Classifier:Enabled");
            var apiKey = configuration["Classifier:ApiKey"];
            var model = configuration["Classifier:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-4.1-nano";
            }

            if (enabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddSingleton(new ChatClient(model: model, apiKey: apiKey));
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
