using System.Text.Json;
using System.Text.Json.Serialization;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    // Vendor boundary. Suggests curated recipe tags from a recipe's name + description + ingredients,
    // using strict Structured Outputs whose allowed values are interpolated from RecipeTag so they
    // can't drift. Refusals / empties / errors map to an empty list (a valid "no suggestions"). The
    // model's reasoning is logged for diagnostics only, never persisted nor returned.
    public sealed class OpenAiRecipeTagSuggester : IRecipeTagSuggester
    {
        // "reasoning" is FIRST (strict outputs generate fields in schema order → cheap chain-of-thought).
        private static readonly BinaryData Schema = BinaryData.FromString($$"""
            {
                "type": "object",
                "properties": {
                    "reasoning": { "type": "string" },
                    "tags": {
                        "type": "array",
                        "items": {
                            "type": "string",
                            "enum": [{{string.Join(", ", Enum.GetNames<RecipeTag>().Select(n => $"\"{n}\""))}}]
                        }
                    }
                },
                "required": ["reasoning", "tags"],
                "additionalProperties": false
            }
            """);

        private static readonly string SystemPrompt =
            "You assign curated category tags to a household recipe. You are given the recipe name, an optional description, and the ingredient lines (English or German).\n" +
            "Choose only tags that clearly apply. It is fine to return an empty list when nothing is confident.\n" +
            "Course tags (pick at most one or two that fit): Breakfast, Starter, Main, Side, Salad, Soup, Dessert, Snack, Drink, Sauce, Baking, Bread.\n" +
            "Dietary tags (only when the ingredients clearly support it): Vegetarian (no meat/fish), Vegan (no animal products at all), GlutenFree, DairyFree (no dairy at all), LactoseFree (low/no lactose but may contain dairy proteins), LowCarb.\n" +
            "Do not guess dietary tags from the name alone — require ingredient evidence. Do not invent tags outside the provided enum.\n" +
            "In 'reasoning', briefly justify your choices in one short English sentence regardless of input language.\n" +
            "Respond only via the provided JSON schema.";

        private static readonly SystemChatMessage SystemMessage = new(SystemPrompt);

        private static readonly ChatCompletionOptions Options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "recipe_tag_suggestion",
                jsonSchema: Schema,
                jsonSchemaIsStrict: true),
        };

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private sealed record SuggesterResponse(string Reasoning, RecipeTag[] Tags);

        private readonly ChatClient _client;
        private readonly ILogger<OpenAiRecipeTagSuggester> _logger;

        public OpenAiRecipeTagSuggester(
            [FromKeyedServices(AiKeys.RecipeTagSuggester)] ChatClient client,
            ILogger<OpenAiRecipeTagSuggester> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<IReadOnlyList<RecipeTag>> SuggestAsync(
            string name, string? description, IReadOnlyList<string> ingredients, CancellationToken ct)
        {
            var prompt =
                $"Name: {name}\n" +
                $"Description: {(string.IsNullOrWhiteSpace(description) ? "(none)" : description)}\n" +
                $"Ingredients:\n{(ingredients.Count == 0 ? "(none)" : string.Join("\n", ingredients.Select(i => "- " + i)))}";

            var messages = new ChatMessage[] { SystemMessage, new UserChatMessage(prompt) };

            try
            {
                var completion = await _client.CompleteChatAsync(messages, Options, ct);

                if (completion.Value.Content.Count == 0
                    || string.IsNullOrWhiteSpace(completion.Value.Content[0].Text))
                {
                    _logger.LogWarning("Tag suggester returned no usable content for '{Name}'.", name);
                    return [];
                }

                var dto = JsonSerializer.Deserialize<SuggesterResponse>(completion.Value.Content[0].Text, JsonOptions);
                if (dto is null)
                {
                    return [];
                }

                // Defensive: distinct + drop anything not a defined enum value (strict schema should
                // prevent this, but never trust the model).
                var tags = dto.Tags
                    .Where(t => Enum.IsDefined(t))
                    .Distinct()
                    .ToList();

                _logger.LogInformation(
                    "Suggested tags for '{Name}': {Tags} ({Reasoning})",
                    name, string.Join(",", tags), dto.Reasoning);

                return tags;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Tag suggester call failed for '{Name}'.", name);
                return [];
            }
        }
    }
}
