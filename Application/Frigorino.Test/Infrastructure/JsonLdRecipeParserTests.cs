using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class JsonLdRecipeParserTests
    {
        private static string Html(string jsonLd)
            => $"<html><head><script type=\"application/ld+json\">{jsonLd}</script></head><body></body></html>";

        [Fact]
        public void Parses_single_recipe_object()
        {
            var html = Html("""
                {"@context":"https://schema.org","@type":"Recipe","name":"Pancakes",
                 "description":"Fluffy &amp; quick","recipeYield":"4 servings",
                 "recipeIngredient":["200g flour","2 eggs"]}
            """);

            var result = JsonLdRecipeParser.Parse(html);

            Assert.NotNull(result);
            Assert.Equal("Pancakes", result!.Name);
            Assert.Equal("Fluffy & quick", result.Description);
            Assert.Equal(4, result.Servings);
            Assert.Equal(new[] { "200g flour", "2 eggs" }, result.Ingredients);
        }

        [Fact]
        public void Finds_recipe_inside_graph_array()
        {
            var html = Html("""
                {"@context":"https://schema.org","@graph":[
                  {"@type":"WebSite","name":"Blog"},
                  {"@type":["Recipe","Thing"],"name":"Soup","recipeIngredient":["water","salt"]}]}
            """);

            var result = JsonLdRecipeParser.Parse(html);

            Assert.NotNull(result);
            Assert.Equal("Soup", result!.Name);
            Assert.Equal(new[] { "water", "salt" }, result.Ingredients);
        }

        [Fact]
        public void Finds_recipe_in_top_level_array_and_reads_legacy_ingredients()
        {
            var html = Html("""
                [{"@type":"Organization","name":"X"},
                 {"@type":"Recipe","name":"Salad","ingredients":["lettuce","oil"]}]
            """);

            var result = JsonLdRecipeParser.Parse(html);

            Assert.NotNull(result);
            Assert.Equal("Salad", result!.Name);
            Assert.Equal(new[] { "lettuce", "oil" }, result.Ingredients);
        }

        [Fact]
        public void Parses_numeric_recipeYield()
        {
            var html = Html("""{"@type":"Recipe","name":"R","recipeYield":6,"recipeIngredient":["a"]}""");
            Assert.Equal(6, JsonLdRecipeParser.Parse(html)!.Servings);
        }

        [Fact]
        public void Caps_ingredients_at_100()
        {
            var many = string.Join(",", Enumerable.Range(0, 150).Select(i => $"\"item {i}\""));
            var html = Html($$"""{"@type":"Recipe","name":"R","recipeIngredient":[{{many}}]}""");
            Assert.Equal(100, JsonLdRecipeParser.Parse(html)!.Ingredients.Count);
        }

        [Fact]
        public void Returns_null_when_no_recipe_node()
        {
            Assert.Null(JsonLdRecipeParser.Parse(Html("""{"@type":"WebPage","name":"x"}""")));
        }

        [Fact]
        public void Returns_null_when_recipe_has_no_ingredients()
        {
            Assert.Null(JsonLdRecipeParser.Parse(Html("""{"@type":"Recipe","name":"Empty"}""")));
        }

        [Fact]
        public void Returns_null_for_malformed_json_and_missing_block()
        {
            Assert.Null(JsonLdRecipeParser.Parse("<html><script type=\"application/ld+json\">{ not json </script></html>"));
            Assert.Null(JsonLdRecipeParser.Parse("<html><body>no jsonld here</body></html>"));
        }
    }
}
