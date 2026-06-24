using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    public static class RecipeTagSuggestionDependencyInjection
    {
        // Standalone — depends on no other AI port. Gated on Ai:ApiKey + Ai:RecipeTagSuggester:Enabled.
        // Registers IRecipeTagSuggester on BOTH paths (real or Null), so the suggest slice can always
        // resolve it. Synchronous on-demand suggester — no job/trigger/queue.
        public static IServiceCollection AddRecipeTagSuggestion(
            this IServiceCollection services, IConfiguration configuration)
        {
            var enabled = configuration.GetValue<bool>("Ai:RecipeTagSuggester:Enabled");
            var apiKey = configuration["Ai:ApiKey"];
            var model = configuration["Ai:RecipeTagSuggester:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                // Deliberately the full model, not a mini/nano: tag suggestion is low-volume and
                // accuracy-sensitive (the whole point of a per-feature model knob). See appsettings.
                model = "gpt-5.4";
            }

            if (enabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddKeyedSingleton<ChatClient>(
                    AiKeys.RecipeTagSuggester, new ChatClient(model: model, apiKey: apiKey));
                services.AddScoped<IRecipeTagSuggester, OpenAiRecipeTagSuggester>();
            }
            else
            {
                services.AddScoped<IRecipeTagSuggester, NullRecipeTagSuggester>();
            }

            return services;
        }
    }
}
