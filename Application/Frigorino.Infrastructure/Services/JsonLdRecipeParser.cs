using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Infrastructure.Services
{
    // Pure, deterministic parse of schema.org/Recipe JSON-LD out of an HTML page. No network, no AI.
    public static class JsonLdRecipeParser
    {
        private const int MaxIngredients = 100;

        // ponytail: regex script-tag extraction — swap to AngleSharp if it proves fragile.
        private static readonly Regex ScriptBlock = new(
            "<script[^>]*type\\s*=\\s*[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new("\\s+", RegexOptions.Compiled);
        private static readonly Regex FirstInt = new("\\d+", RegexOptions.Compiled);

        public static ImportedRecipe? Parse(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            foreach (Match block in ScriptBlock.Matches(html))
            {
                var json = block.Groups[1].Value.Trim();
                if (json.Length == 0)
                {
                    continue;
                }

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(json);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (doc)
                {
                    foreach (var node in EnumerateNodes(doc.RootElement))
                    {
                        if (!IsRecipeNode(node))
                        {
                            continue;
                        }
                        var mapped = MapRecipe(node);
                        if (mapped is not null)
                        {
                            return mapped;
                        }
                    }
                }
            }

            return null;
        }

        private static IEnumerable<JsonElement> EnumerateNodes(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray())
                {
                    foreach (var n in EnumerateNodes(el))
                    {
                        yield return n;
                    }
                }
                yield break;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                yield return root;
                if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in graph.EnumerateArray())
                    {
                        foreach (var n in EnumerateNodes(el))
                        {
                            yield return n;
                        }
                    }
                }
            }
        }

        private static bool IsRecipeNode(JsonElement node)
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("@type", out var type))
            {
                return false;
            }
            if (type.ValueKind == JsonValueKind.String)
            {
                return string.Equals(type.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase);
            }
            if (type.ValueKind == JsonValueKind.Array)
            {
                return type.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String
                    && string.Equals(t.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        private static ImportedRecipe? MapRecipe(JsonElement node)
        {
            var name = CleanText(GetString(node, "name"));
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            if (name.Length > Recipe.NameMaxLength)
            {
                name = name[..Recipe.NameMaxLength];
            }

            var description = CleanText(StripTags(GetString(node, "description")));
            if (description is not null && description.Length > Recipe.DescriptionMaxLength)
            {
                description = description[..Recipe.DescriptionMaxLength];
            }

            var ingredients = ReadIngredients(node);
            if (ingredients.Count == 0)
            {
                return null;
            }

            return new ImportedRecipe(name, description, ParseServings(node), ingredients, CleanText(GetString(node, "author")));
        }

        private static List<string> ReadIngredients(JsonElement node)
        {
            var list = new List<string>();
            if (!TryGetArray(node, "recipeIngredient", out var arr) && !TryGetArray(node, "ingredients", out arr))
            {
                return list;
            }
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                var text = CleanText(el.GetString());
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }
                if (text.Length > RecipeItem.TextMaxLength)
                {
                    text = text[..RecipeItem.TextMaxLength];
                }
                list.Add(text);
                if (list.Count >= MaxIngredients)
                {
                    break;
                }
            }
            return list;
        }

        private static int? ParseServings(JsonElement node)
        {
            if (!node.TryGetProperty("recipeYield", out var y))
            {
                return null;
            }
            string? raw = y.ValueKind switch
            {
                JsonValueKind.Number => y.ToString(),
                JsonValueKind.String => y.GetString(),
                JsonValueKind.Array => y.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString())
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
                _ => null,
            };
            if (raw is null)
            {
                return null;
            }
            var match = FirstInt.Match(raw);
            if (!match.Success || !int.TryParse(match.Value, out var n) || n < 1 || n > Recipe.ServingsMax)
            {
                return null;
            }
            return n;
        }

        private static bool TryGetArray(JsonElement node, string prop, out JsonElement arr)
        {
            if (node.TryGetProperty(prop, out arr) && arr.ValueKind == JsonValueKind.Array)
            {
                return true;
            }
            arr = default;
            return false;
        }

        private static string? GetString(JsonElement node, string prop)
            => node.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static string? StripTags(string? s) => s is null ? null : HtmlTag.Replace(s, " ");

        private static string? CleanText(string? s)
        {
            if (s is null)
            {
                return null;
            }
            var cleaned = Whitespace.Replace(WebUtility.HtmlDecode(s), " ").Trim();
            return cleaned.Length == 0 ? null : cleaned;
        }
    }
}
