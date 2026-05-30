using System.Text.Json;
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

        private static readonly BinaryData Schema = BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "expiryHandling": {
                        "type": "string",
                        "enum": ["NonPerishable", "UserEntersFromPackage", "AiRecommendsShelfLife"]
                    },
                    "defaultShelfLifeDays": {
                        "type": ["integer", "null"],
                        "minimum": 1,
                        "maximum": 365
                    }
                },
                "required": ["expiryHandling", "defaultShelfLifeDays"],
                "additionalProperties": false
            }
            """u8.ToArray());

        private const string SystemPrompt =
            "You classify how a grocery/household product expires. Choose exactly one expiryHandling:\n" +
            "- NonPerishable: effectively never expires (e.g. salt/Salz, sugar/Zucker, dish soap/Spülmittel). defaultShelfLifeDays = null.\n" +
            "- UserEntersFromPackage: perishable with a printed date the user should read (e.g. yogurt/Joghurt, packaged meat/abgepacktes Fleisch). defaultShelfLifeDays = null.\n" +
            "- AiRecommendsShelfLife: perishable with a predictable typical shelf life you can estimate in days (e.g. fresh milk/Frischmilch ~7, bananas/Bananen ~5, lettuce/Salat ~4). defaultShelfLifeDays = that estimate, 1..365.\n" +
            "Respond only via the provided JSON schema.";

        private readonly ChatClient _client;
        private readonly ILogger<OpenAiItemClassifier> _logger;

        public OpenAiItemClassifier(ChatClient client, ILogger<OpenAiItemClassifier> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct)
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "product_classification",
                    jsonSchema: Schema,
                    jsonSchemaIsStrict: true),
            };

            var messages = new ChatMessage[]
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(normalizedName),
            };

            try
            {
                var completion = await _client.CompleteChatAsync(messages, options, ct);

                // Refusal or empty content → treat as non-perishable rather than failing the job.
                if (completion.Value.Content.Count == 0
                    || string.IsNullOrWhiteSpace(completion.Value.Content[0].Text))
                {
                    _logger.LogWarning(
                        "Classifier returned no usable content for '{Name}'; defaulting to non-perishable.",
                        normalizedName);
                    return Result.Ok(new ProductClassification(ExpiryProfile.NonPerishable));
                }

                using var json = JsonDocument.Parse(completion.Value.Content[0].Text);
                var root = json.RootElement;

                var handling = Enum.Parse<ExpiryHandling>(root.GetProperty("expiryHandling").GetString()!);
                int? days = root.GetProperty("defaultShelfLifeDays").ValueKind == JsonValueKind.Null
                    ? null
                    : root.GetProperty("defaultShelfLifeDays").GetInt32();

                var profile = ExpiryProfile.Create(handling, days);
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
