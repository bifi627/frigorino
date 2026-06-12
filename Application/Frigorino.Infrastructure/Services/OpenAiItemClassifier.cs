using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    // Vendor boundary. Uses the official OpenAI SDK directly with strict Structured Outputs.
    // Swapping vendor later = rewrite this one class behind the unchanged IItemClassifier port.
    public class OpenAiItemClassifier : IItemClassifier
    {
        // Bump when the prompt or schema changes to force re-classification on the next reference.
        public int Version => 2;

        // The strict Structured Outputs schema. Enum values and the shelf-life bounds are
        // interpolated from the domain types so they can't silently drift from ProductCategory /
        // ExpiryHandling / ExpiryProfile — only the JSON skeleton is hand-written.
        // "reasoning" is listed FIRST deliberately: strict outputs generate fields in schema order,
        // so the model writes its rationale before committing to the labels (cheap chain-of-thought).
        // It is a diagnostic only — logged, never persisted nor returned across IItemClassifier.
        private static readonly BinaryData Schema = BinaryData.FromString($$"""
            {
                "type": "object",
                "properties": {
                    "reasoning": {
                        "type": "string"
                    },
                    "productCategory": {
                        "type": "string",
                        "enum": [{{string.Join(", ", Enum.GetNames<ProductCategory>().Select(n => $"\"{n}\""))}}]
                    },
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
                "required": ["reasoning", "productCategory", "expiryHandling", "defaultShelfLifeDays"],
                "additionalProperties": false
            }
            """);

        private static readonly string SystemPrompt =
            "You classify a single item a user wrote on a household list. Items are usually groceries or household supplies, but may be anything (e.g. a reminder, or a non-consumable object).\n" +
            "In 'reasoning', briefly justify your choice in one short sentence always in english not matter the input language.\n" +
            "Set 'productCategory' to exactly one supermarket aisle — pick the single aisle where the item is normally shopped/stored:\n" +
            "- Unknown: you cannot classify it (nonsense text, placeholders, invalid characters, emotes).\n" +
            "- Other: a recognized thing that is NOT a stocked grocery/household good (e.g. a task like 'call dentist'/'Zahnarzt anrufen', or a one-off object).\n" +
            "- Produce: fresh fruit & vegetables (e.g. bananas/Bananen, lettuce/Salat, apples/Äpfel).\n" +
            "- Bakery: bread & baked goods (e.g. bread/Brot, rolls/Brötchen, croissants).\n" +
            "- Meat: fresh or packaged meat & poultry (e.g. chicken/Hähnchen, mince/Hackfleisch).\n" +
            "- Fish: fresh or packaged fish & seafood (e.g. salmon/Lachs, shrimp/Garnelen).\n" +
            "- DairyAndEggs: milk, butter, yogurt, eggs (e.g. milk/Milch, yogurt/Joghurt, eggs/Eier).\n" +
            "- Cheese: cheese of any kind (e.g. Gouda, cream cheese/Frischkäse, Parmesan).\n" +
            "- DeliAndColdCuts: chilled sausage, sliced cold cuts, deli counter (e.g. ham/Schinken, salami/Salami, Aufschnitt).\n" +
            "- Frozen: frozen foods (e.g. frozen pizza/Tiefkühlpizza, ice cream/Eis, frozen vegetables/TK-Gemüse).\n" +
            "- Pantry: dry shelf-stable staples (e.g. pasta/Nudeln, rice/Reis, flour/Mehl, sugar/Zucker).\n" +
            "- CannedGoods: canned or jarred goods (e.g. canned tomatoes/Dosentomaten, beans/Bohnen, corn/Mais).\n" +
            "- Sauces: sauces, dressings & pastes (e.g. ketchup/Ketchup, pasta sauce/Passata, mayonnaise/Mayonnaise, pesto/Pesto).\n" +
            "- OilsAndVinegar: cooking oils & vinegar (e.g. olive oil/Olivenöl, sunflower oil/Sonnenblumenöl, vinegar/Essig).\n" +
            "- Spices: spices, salt & baking aids (e.g. salt/Salz, pepper/Pfeffer, cinnamon/Zimt, baking powder/Backpulver).\n" +
            "- Cereal: breakfast cereal & oats (e.g. muesli/Müsli, oats/Haferflocken, cornflakes/Cornflakes).\n" +
            "- Spreads: sweet or savoury spreads (e.g. jam/Marmelade, Nutella, honey/Honig, peanut butter/Erdnussbutter).\n" +
            "- Snacks: savoury snacks (e.g. chips/Chips, pretzels/Brezeln, crackers/Cracker, nuts/Nüsse).\n" +
            "- Sweets: confectionery (e.g. chocolate/Schokolade, gummy bears/Gummibärchen, candy/Bonbons).\n" +
            "- Beverages: non-alcoholic drinks (e.g. water/Wasser, juice/Saft, soda/Limonade, coffee/Kaffee, tea/Tee).\n" +
            "- Alcohol: alcoholic drinks (e.g. beer/Bier, wine/Wein, spirits/Schnaps).\n" +
            "- HouseholdAndCleaning: cleaning & household supplies (e.g. dish soap/Spülmittel, paper towels/Küchenrolle, batteries/Batterien).\n" +
            "- HealthAndBeauty: personal care, health & cosmetics (e.g. toothpaste/Zahnpasta, shampoo/Shampoo, plasters/Pflaster).\n" +
            "- Baby: baby food & baby care (e.g. baby food/Babybrei, diapers/Windeln, baby wipes/Feuchttücher).\n" +
            "- Pet: pet supplies & pet food (e.g. dog food/Hundefutter, cat litter/Katzenstreu).\n" +
            "Set 'expiryHandling' to exactly one of:\n" +
            "- Unknown: anything you do not recognize as an item (e. g. nonsense text, placeholders, invalid characters, emotes)\n" +
            "- NonPerishable: effectively never expires (e.g. salt/Salz, sugar/Zucker, dish soap/Spülmittel). defaultShelfLifeDays = null.\n" +
            "- UserEntersFromPackage: perishable with a printed date the user should read (e.g. yogurt/Joghurt, packaged meat/abgepacktes Fleisch). defaultShelfLifeDays = null.\n" +
            $"- AiRecommendsShelfLife: perishable with a predictable typical shelf life you can estimate in days (e.g. fresh milk/Frischmilch ~7, bananas/Bananen ~5, lettuce/Salat ~4). defaultShelfLifeDays = that estimate, {ExpiryProfile.ShelfLifeDaysMin}..{ExpiryProfile.ShelfLifeDaysMax}.\n" +
            "For Other items, use Unknown with defaultShelfLifeDays = null.\n" +
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

        // The schema mirrors this shape exactly; STJ maps the string enums and nullable int directly.
        // Reasoning is diagnostic only — logged below, never persisted nor returned across the port.
        private sealed record ClassifierResponse(
            string Reasoning, ProductCategory ProductCategory, ExpiryHandling ExpiryHandling, int? DefaultShelfLifeDays);

        private readonly ChatClient _client;
        private readonly ILogger<OpenAiItemClassifier> _logger;

        public OpenAiItemClassifier(
            [FromKeyedServices(AiKeys.Classifier)] ChatClient client,
            ILogger<OpenAiItemClassifier> logger)
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

                // Refusal or empty content → treat as Unknown/non-perishable rather than failing the job.
                if (completion.Value.Content.Count == 0
                    || string.IsNullOrWhiteSpace(completion.Value.Content[0].Text))
                {
                    _logger.LogWarning(
                        "Classifier returned no usable content for '{Name}'; defaulting to Unknown/non-perishable.",
                        normalizedName);
                    return Result.Ok(new ProductClassification(ProductCategory.Unknown, ExpiryProfile.NonPerishable));
                }

                var dto = JsonSerializer.Deserialize<ClassifierResponse>(completion.Value.Content[0].Text, JsonOptions);

                var profile = dto is null
                    ? Result.Fail<ExpiryProfile>("Empty classifier payload.")
                    : ExpiryProfile.Create(dto.ExpiryHandling, dto.DefaultShelfLifeDays);

                // Log the model's reasoning as a diagnostic — it does not leave this method.
                _logger.LogInformation(
                    "Classified '{Name}' as {Category}/{Handling} (shelf life {Days}): {Reasoning} ({Total} Total Tokens used, {ReasoningTokens} Reasoning Token used)",
                    normalizedName, dto?.ProductCategory, dto?.ExpiryHandling, dto?.DefaultShelfLifeDays, dto?.Reasoning, completion.Value.Usage.TotalTokenCount, completion.Value.Usage.OutputTokenDetails.ReasoningTokenCount);

                if (dto is null || profile.IsFailed)
                {
                    // Model produced a schema-valid-but-semantically-inconsistent combination; be safe.
                    return Result.Ok(new ProductClassification(ProductCategory.Unknown, ExpiryProfile.NonPerishable));
                }


                return Result.Ok(new ProductClassification(dto.ProductCategory, profile.Value));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Classifier call failed for '{Name}'.", normalizedName);
                return Result.Fail<ProductClassification>("Classifier call failed.");
            }
        }
    }
}
