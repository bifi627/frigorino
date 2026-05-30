using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    // Vendor boundary. Uses the official OpenAI SDK directly with strict Structured Outputs.
    // Swapping vendor later = rewrite this one class behind the unchanged IItemClassifier port.
    public class OpenAiItemClassifier : IItemClassifier
    {
        // Bump when the prompt or schema changes to force re-classification on the next reference.
        public int Version => 1;

        // The strict Structured Outputs schema. Enum values and the shelf-life bounds are
        // interpolated from the domain types so they can't silently drift from ExpiryHandling /
        // ExpiryProfile — only the JSON skeleton is hand-written.
        private static readonly BinaryData Schema = BinaryData.FromString($$"""
            {
                "type": "object",
                "properties": {
                    "expiryHandling": {
                        "type": "string",
                        "enum": [{{string.Join(", ", Enum.GetNames<ExpiryHandling>().Select(n => $"\"{n}\""))}}]
                    },
                    "defaultShelfLifeDays": {
                        "type": ["integer", "null"],
                        "minimum": {{ExpiryProfile.ShelfLifeDaysMin}},
                        "maximum": {{ExpiryProfile.ShelfLifeDaysMax}}
                    }
                },
                "required": ["expiryHandling", "defaultShelfLifeDays"],
                "additionalProperties": false
            }
            """);

        private static readonly string SystemPrompt =
            "You classify how a grocery/household product expires. Choose exactly one expiryHandling:\n" +
            "- NonPerishable: effectively never expires (e.g. salt/Salz, sugar/Zucker, dish soap/Spülmittel). defaultShelfLifeDays = null.\n" +
            "- UserEntersFromPackage: perishable with a printed date the user should read (e.g. yogurt/Joghurt, packaged meat/abgepacktes Fleisch). defaultShelfLifeDays = null.\n" +
            $"- AiRecommendsShelfLife: perishable with a predictable typical shelf life you can estimate in days (e.g. fresh milk/Frischmilch ~7, bananas/Bananen ~5, lettuce/Salat ~4). defaultShelfLifeDays = that estimate, {ExpiryProfile.ShelfLifeDaysMin}..{ExpiryProfile.ShelfLifeDaysMax}.\n" +
            "Respond only via the provided JSON schema.";

        // Per-request config is invariant (same schema + system prompt every call), so build once.
        private static readonly SystemChatMessage SystemMessage = new(SystemPrompt);

        private static readonly ChatCompletionOptions Options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "product_classification",
                jsonSchema: Schema,
                jsonSchemaIsStrict: true),
        };

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        // The schema mirrors this shape exactly; STJ maps the string enum and nullable int directly.
        private sealed record ClassifierResponse(ExpiryHandling ExpiryHandling, int? DefaultShelfLifeDays);

        private readonly ChatClient _client;
        private readonly ILogger<OpenAiItemClassifier> _logger;

        public OpenAiItemClassifier(ChatClient client, ILogger<OpenAiItemClassifier> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct)
        {
            var messages = new ChatMessage[] { SystemMessage, new UserChatMessage(normalizedName) };

            try
            {
                var completion = await _client.CompleteChatAsync(messages, Options, ct);

                // Refusal or empty content → treat as non-perishable rather than failing the job.
                if (completion.Value.Content.Count == 0
                    || string.IsNullOrWhiteSpace(completion.Value.Content[0].Text))
                {
                    _logger.LogWarning(
                        "Classifier returned no usable content for '{Name}'; defaulting to non-perishable.",
                        normalizedName);
                    return Result.Ok(new ProductClassification(ExpiryProfile.NonPerishable));
                }

                var dto = JsonSerializer.Deserialize<ClassifierResponse>(completion.Value.Content[0].Text, JsonOptions);

                var profile = dto is null
                    ? Result.Fail<ExpiryProfile>("Empty classifier payload.")
                    : ExpiryProfile.Create(dto.ExpiryHandling, dto.DefaultShelfLifeDays);
                if (profile.IsFailed)
                {
                    // Model produced a schema-valid-but-semantically-inconsistent combination; be safe.
                    return Result.Ok(new ProductClassification(ExpiryProfile.NonPerishable));
                }

                return Result.Ok(new ProductClassification(profile.Value));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Classifier call failed for '{Name}'.", normalizedName);
                return Result.Fail<ProductClassification>("Classifier call failed.");
            }
        }
    }
}
