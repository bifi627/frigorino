using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    // Vendor boundary. Cheap-model inline extraction of {clean name, quantity} from a user's raw
    // list-item text. Swapping vendor later = rewrite this one class behind IQuantityExtractor.
    public class OpenAiQuantityExtractor : IQuantityExtractor
    {
        // "reasoning" is FIRST (strict outputs generate fields in schema order → cheap CoT).
        // quantityUnit is always required (strict); when quantityValue is null we ignore the unit.
        // Unit enum names are interpolated from QuantityUnit so they can't drift.
        private static readonly BinaryData Schema = BinaryData.FromString($$"""
            {
                "type": "object",
                "properties": {
                    "reasoning": { "type": "string" },
                    "cleanName": { "type": "string" },
                    "quantityValue": { "type": ["number", "null"] },
                    "quantityUnit": {
                        "type": "string",
                        "enum": [{{string.Join(", ", Enum.GetNames<QuantityUnit>().Select(n => $"\"{n}\""))}}]
                    }
                },
                "required": ["reasoning", "cleanName", "quantityValue", "quantityUnit"],
                "additionalProperties": false
            }
            """);

        private static readonly string SystemPrompt =
            "You extract the product name and quantity a user wrote on a household list. Inputs may be English or German, and the quantity may come before or after the name, or be absent.\n" +
            "Set 'cleanName' to the item with any quantity/amount removed (e.g. '20 apples'/'apples 20' -> 'apples'; '1l milk' -> 'milk'; '500g Mehl' -> 'Mehl'; '2 bottles of beer' -> 'beer').\n" +
            "Preserve the product name's original casing and wording exactly as the user typed it; only strip the quantity/amount. Do NOT lowercase, capitalize, translate, or reword it (e.g. '2 Bananas' -> 'Bananas'; 'Organic MILK 1l' -> 'Organic MILK'; 'Coca Cola 2' -> 'Coca Cola').\n" +
            "Set 'quantityValue' to the numeric amount, or null if there is none. The amount may be written as digits OR spelled out as a word in either language — convert words to a number (e.g. 'two cups of coffee' -> 'coffee', quantityValue 2; 'zwei Liter Cola' -> 'Cola', quantityValue 2, Liter; 'fünf Flaschen Wasser' -> 'Wasser', quantityValue 5, Bottle; '2 Packungen Honig' -> 'Honig', quantityValue 2, Pack).\n" +
            "A digit that is part of a brand/name is NOT a quantity (e.g. '7up' -> cleanName '7up', quantityValue null; 'WD-40' -> 'WD-40', null; 'E45 cream' -> 'E45 cream', null).\n" +
            "Set 'quantityUnit' to the best-fitting unit: Gram/Kilogram for weight, Milliliter/Liter for volume, Bottle/Can/Pack/Bag for containers, Piece for a bare count. When quantityValue is null, still pick Piece (it is ignored).\n" +
            "In 'reasoning', briefly justify your choice in one short English sentence regardless of input language.\n" +
            "Respond only via the provided JSON schema.";

        private static readonly SystemChatMessage SystemMessage = new(SystemPrompt);

        private static readonly ChatCompletionOptions Options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "quantity_extraction",
                jsonSchema: Schema,
                jsonSchemaIsStrict: true),
        };

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private sealed record ExtractorResponse(
            string Reasoning, string CleanName, decimal? QuantityValue, QuantityUnit QuantityUnit);

        private readonly ChatClient _client;
        private readonly ILogger<OpenAiQuantityExtractor> _logger;

        public OpenAiQuantityExtractor(
            [FromKeyedServices(AiKeys.Extractor)] ChatClient client,
            ILogger<OpenAiQuantityExtractor> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<Result<QuantityExtraction>> ExtractAsync(string rawText, CancellationToken ct)
        {
            var messages = new ChatMessage[] { SystemMessage, new UserChatMessage(rawText) };

            try
            {
                var completion = await _client.CompleteChatAsync(messages, Options, ct);

                // Refusal / empty → no extraction; keep the raw text, no quantity.
                if (completion.Value.Content.Count == 0
                    || string.IsNullOrWhiteSpace(completion.Value.Content[0].Text))
                {
                    _logger.LogWarning("Extractor returned no usable content for '{Raw}'; keeping raw text.", rawText);
                    return Result.Ok(new QuantityExtraction(rawText, null));
                }

                var dto = JsonSerializer.Deserialize<ExtractorResponse>(completion.Value.Content[0].Text, JsonOptions);
                if (dto is null)
                {
                    return Result.Ok(new QuantityExtraction(rawText, null));
                }

                Quantity? quantity = null;
                if (dto.QuantityValue is decimal v && v > 0)
                {
                    var q = Quantity.Create(v, dto.QuantityUnit);
                    if (q.IsSuccess)
                    {
                        quantity = q.Value;
                    }
                }

                var cleanName = string.IsNullOrWhiteSpace(dto.CleanName) ? rawText : dto.CleanName.Trim();

                _logger.LogInformation(
                    "Extracted '{Raw}' -> name '{Name}', qty {Value}/{Unit}: {Reasoning}",
                    rawText, cleanName, dto.QuantityValue, dto.QuantityUnit, dto.Reasoning);

                return Result.Ok(new QuantityExtraction(cleanName, quantity));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Extractor call failed for '{Raw}'.", rawText);
                return Result.Fail<QuantityExtraction>("Extractor call failed.");
            }
        }
    }
}
