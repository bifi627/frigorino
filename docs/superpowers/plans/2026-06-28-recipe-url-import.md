# Recipe URL Import (JSON-LD) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user paste a recipe URL and have the app create a recipe (name, servings, description, ingredients, source link) from the page's `schema.org/Recipe` JSON-LD, then land on the existing edit page to review.

**Architecture:** One Infrastructure service (`RecipeImportService`) does a server-side, SSRF-hardened fetch and a tolerant JSON-LD parse, returning a plain `ImportedRecipe` record. One vertical slice (`ImportRecipe`) maps that record onto the existing `Recipe.Create`/`AddSection`/`AddItem`/`AddLink` aggregate methods, fires the recipe quantity-extraction trigger per item, and returns the recipe. The frontend adds an import dialog on the recipes overview that calls the slice and navigates to the edit page. No new entity, no migration, no AI, no interface/config gate.

**Tech Stack:** .NET 10 minimal-API vertical slices, EF Core (Postgres), FluentResults, `System.Text.Json`, `SocketsHttpHandler`; React 19 + TanStack Query/Router + MUI; Reqnroll + Playwright + Postgres Testcontainers for integration tests; xUnit + FakeItEasy for unit tests.

## Global Constraints

- **Slices only** — one file = one endpoint with colocated request/response DTOs; never add controllers. Reads are handler-only inline EF projection.
- **C# brace style** — always block-style `{}`, even for single-line conditions/namespaces.
- **No AI, no `IRecipeImporter` interface, no config gate, no `Null` impl** — the deterministic JSON-LD path is always on; the slice calls the concrete `RecipeImportService` directly.
- **No data-model change, no EF migration.**
- **Recipe items never chain product classification** — fire `IRecipeQuantityExtractionTrigger` only (recipes don't create `Product` rows).
- **Enums serialize as string names on the wire** (already configured globally).
- **i18n** — UI uses `t()`; add keys to the existing `recipes` object in `public/locales/{en,de}/translation.json` (existing namespace → JSON only, no `i18next.d.ts` change). **Tests never assert on translated text** — testids / `data-*` only.
- **Frontend tooling via npm scripts** — `npm run tsc` / `lint` / `prettier` / `build` / `api`; never raw `npx`. Regenerate the client with `npm run api` from `ClientApp/` after the slice lands; commit generated `src/lib/api/` output.
- **DB-behavior tests use Testcontainers** in `Frigorino.IntegrationTests` (Reqnroll + Playwright); `Frigorino.Test` is pure unit/aggregate/slice-logic only — no EF InMemory for new coverage.
- **Reqnroll gotchas:** step text must be globally unique across the IT assembly; a step reused under a different keyword needs `[Given]`+`[When]` double-decoration; assert on testids only; run `npm run build` before the IT (the harness serves `ClientApp/build`).
- **Verification gate (final):** `npm run tsc` + `npm run lint` + `npm run prettier` + `npm run build` + `dotnet test Application/Frigorino.sln` + `docker build -f Application/Dockerfile -t frigorino .`.
- **Branch:** `feat/recipe-url-import` (already created off `stage`). No Co-Authored-By trailers.

---

# Phase 1 — Backend (parse, guard, service, slice, API integration test)

### Task 1: JSON-LD parser + `ImportedRecipe`

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/ImportedRecipe.cs`
- Create: `Application/Frigorino.Infrastructure/Services/JsonLdRecipeParser.cs`
- Test: `Application/Frigorino.Test/Infrastructure/JsonLdRecipeParserTests.cs`

**Interfaces:**
- Produces: `record ImportedRecipe(string Name, string? Description, int? Servings, IReadOnlyList<string> Ingredients, string? SourceName)` and `static ImportedRecipe? JsonLdRecipeParser.Parse(string html)`.

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Infrastructure/JsonLdRecipeParserTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~JsonLdRecipeParserTests"`
Expected: FAIL — `JsonLdRecipeParser`/`ImportedRecipe` do not exist (compile error).

- [ ] **Step 3: Implement `ImportedRecipe`**

Create `Application/Frigorino.Infrastructure/Services/ImportedRecipe.cs`:

```csharp
namespace Frigorino.Infrastructure.Services
{
    // Plain data carrier produced by RecipeImportService (NOT an entity). The ImportRecipe slice maps
    // this onto Recipe.Create / AddSection / AddItem / AddLink.
    public sealed record ImportedRecipe(
        string Name,
        string? Description,
        int? Servings,
        IReadOnlyList<string> Ingredients,
        string? SourceName);
}
```

- [ ] **Step 4: Implement `JsonLdRecipeParser`**

Create `Application/Frigorino.Infrastructure/Services/JsonLdRecipeParser.cs`:

```csharp
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
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~JsonLdRecipeParserTests"`
Expected: PASS (8 tests).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/ImportedRecipe.cs Application/Frigorino.Infrastructure/Services/JsonLdRecipeParser.cs Application/Frigorino.Test/Infrastructure/JsonLdRecipeParserTests.cs
git commit -m "feat(recipes): JSON-LD recipe parser"
```

---

### Task 2: SSRF URL/IP guard helper

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/RecipeImportUrl.cs`
- Test: `Application/Frigorino.Test/Infrastructure/RecipeImportUrlTests.cs`

**Interfaces:**
- Produces: `static bool RecipeImportUrl.TryParseHttpUrl(string? raw, out Uri uri)` and `static bool RecipeImportUrl.IsPublicIpAddress(IPAddress address)`.

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Infrastructure/RecipeImportUrlTests.cs`:

```csharp
using System.Net;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class RecipeImportUrlTests
    {
        [Theory]
        [InlineData("https://example.com/recipe", true)]
        [InlineData("http://example.com", true)]
        [InlineData("ftp://example.com/x", false)]
        [InlineData("file:///etc/passwd", false)]
        [InlineData("not a url", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void TryParseHttpUrl_accepts_only_http_and_https(string? raw, bool expected)
        {
            Assert.Equal(expected, RecipeImportUrl.TryParseHttpUrl(raw, out _));
        }

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("10.1.2.3")]
        [InlineData("172.16.5.4")]
        [InlineData("172.31.255.255")]
        [InlineData("192.168.0.1")]
        [InlineData("169.254.169.254")] // cloud metadata
        [InlineData("100.64.0.1")]      // CGNAT
        [InlineData("0.0.0.0")]
        [InlineData("::1")]
        [InlineData("fe80::1")]         // link-local v6
        [InlineData("fc00::1")]         // unique-local v6
        [InlineData("::ffff:10.0.0.1")] // IPv4-mapped private
        public void IsPublicIpAddress_rejects_non_public(string ip)
        {
            Assert.False(RecipeImportUrl.IsPublicIpAddress(IPAddress.Parse(ip)));
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("1.1.1.1")]
        [InlineData("93.184.216.34")]
        [InlineData("2606:4700:4700::1111")]
        public void IsPublicIpAddress_allows_public(string ip)
        {
            Assert.True(RecipeImportUrl.IsPublicIpAddress(IPAddress.Parse(ip)));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeImportUrlTests"`
Expected: FAIL — `RecipeImportUrl` does not exist.

- [ ] **Step 3: Implement `RecipeImportUrl`**

Create `Application/Frigorino.Infrastructure/Services/RecipeImportUrl.cs`:

```csharp
using System.Net;
using System.Net.Sockets;

namespace Frigorino.Infrastructure.Services
{
    // Pure SSRF guards for the server-side recipe fetch.
    public static class RecipeImportUrl
    {
        public static bool TryParseHttpUrl(string? raw, out Uri uri)
        {
            uri = null!;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }
            if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var parsed))
            {
                return false;
            }
            if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }
            uri = parsed;
            return true;
        }

        // True only for globally-routable addresses. Rejects loopback / private / CGNAT / link-local
        // (incl. 169.254.169.254 metadata) / unique-local / unspecified, and IPv4-mapped variants.
        public static bool IsPublicIpAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }
            if (IPAddress.IsLoopback(address)
                || address.Equals(IPAddress.Any)
                || address.Equals(IPAddress.IPv6Any))
            {
                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();
                if (b[0] == 10)
                {
                    return false;
                }
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                {
                    return false;
                }
                if (b[0] == 192 && b[1] == 168)
                {
                    return false;
                }
                if (b[0] == 169 && b[1] == 254)
                {
                    return false;
                }
                if (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                {
                    return false;
                }
                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                {
                    return false;
                }
                var b = address.GetAddressBytes();
                if ((b[0] & 0xFE) == 0xFC)
                {
                    return false;
                }
                return true;
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeImportUrlTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/RecipeImportUrl.cs Application/Frigorino.Test/Infrastructure/RecipeImportUrlTests.cs
git commit -m "feat(recipes): SSRF URL/IP guard for import fetch"
```

---

### Task 3: `RecipeImportService` (guarded fetch + parse) + DI

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/RecipeImportConnect.cs`
- Create: `Application/Frigorino.Infrastructure/Services/RecipeImportService.cs`
- Create: `Application/Frigorino.Infrastructure/Services/RecipeImportDependencyInjection.cs`
- Modify: `Application/Frigorino.Web/Program.cs` (register `AddRecipeImport`)
- Test: `Application/Frigorino.Test/Infrastructure/RecipeImportServiceTests.cs`

**Interfaces:**
- Consumes: `RecipeImportUrl`, `JsonLdRecipeParser`, `ImportedRecipe` (Tasks 1–2).
- Produces: `class RecipeImportService` with `virtual Task<Result<ImportedRecipe>> ImportAsync(string url, CancellationToken ct)`, `static RecipeImportService CreateDefault()`, and a `protected RecipeImportService()` test seam; failures carry `Error.Metadata["code"]` ∈ {`invalid_url`,`fetch_failed`,`no_recipe_found`}. `IServiceCollection.AddRecipeImport()`.

- [ ] **Step 1: Write the failing tests**

These exercise the *real* guarded client without external network: a literal private/loopback IP is rejected by the `ConnectCallback` before any socket connects, so `ImportAsync` returns `fetch_failed`; a non-http URL returns `invalid_url`.

Create `Application/Frigorino.Test/Infrastructure/RecipeImportServiceTests.cs`:

```csharp
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class RecipeImportServiceTests
    {
        private static string? Code(FluentResults.IResultBase r)
            => r.Errors[0].Metadata.TryGetValue("code", out var c) ? c?.ToString() : null;

        [Fact]
        public async Task Rejects_non_http_url_with_invalid_url()
        {
            var service = RecipeImportService.CreateDefault();
            var result = await service.ImportAsync("ftp://example.com/x", CancellationToken.None);
            Assert.True(result.IsFailed);
            Assert.Equal("invalid_url", Code(result));
        }

        [Theory]
        [InlineData("http://127.0.0.1/recipe")]
        [InlineData("http://169.254.169.254/latest/meta-data")]
        [InlineData("http://10.0.0.5/")]
        public async Task Blocks_private_targets_as_fetch_failed(string url)
        {
            var service = RecipeImportService.CreateDefault();
            var result = await service.ImportAsync(url, CancellationToken.None);
            Assert.True(result.IsFailed);
            Assert.Equal("fetch_failed", Code(result));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeImportServiceTests"`
Expected: FAIL — `RecipeImportService` does not exist.

- [ ] **Step 3: Implement the guarded connect callback**

Create `Application/Frigorino.Infrastructure/Services/RecipeImportConnect.cs`:

```csharp
using System.Net;
using System.Net.Sockets;

namespace Frigorino.Infrastructure.Services
{
    internal static class RecipeImportConnect
    {
        // ponytail: ConnectCallback IP check is the load-bearing SSRF defense. It validates the actual
        // resolved IP and connects directly to it, so DNS-rebinding and redirect-to-private are both
        // covered (every connection, including each redirect hop, runs this).
        public static async ValueTask<Stream> ConnectAsync(
            SocketsHttpConnectionContext context, CancellationToken ct)
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            if (addresses.Length == 0 || addresses.Any(a => !RecipeImportUrl.IsPublicIpAddress(a)))
            {
                // Reject the whole host if ANY record is non-public (defeats split-horizon rebinding).
                throw new IOException("Refused to connect to a non-public address.");
            }

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}
```

- [ ] **Step 4: Implement `RecipeImportService`**

Create `Application/Frigorino.Infrastructure/Services/RecipeImportService.cs`:

```csharp
using System.Net;
using FluentResults;

namespace Frigorino.Infrastructure.Services
{
    public class RecipeImportService
    {
        public const long MaxResponseBytes = 3 * 1024 * 1024;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient _http;

        public RecipeImportService(HttpClient http) => _http = http;

        // ponytail: protected ctor is the IT test seam (StubRecipeImportService overrides ImportAsync);
        // avoids a one-impl interface that the spec deliberately omits.
        protected RecipeImportService() => _http = null!;

        public static RecipeImportService CreateDefault() => new(BuildGuardedClient());

        internal static HttpClient BuildGuardedClient()
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectCallback = RecipeImportConnect.ConnectAsync,
            };
            var client = new HttpClient(handler) { Timeout = RequestTimeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Frigorino/1.0; +recipe-import)");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            return client;
        }

        public virtual async Task<Result<ImportedRecipe>> ImportAsync(string url, CancellationToken ct)
        {
            if (!RecipeImportUrl.TryParseHttpUrl(url, out var uri))
            {
                return Fail("invalid_url", "Enter a valid http(s) URL.");
            }

            string html;
            try
            {
                using var resp = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    return Fail("fetch_failed", $"The page returned status {(int)resp.StatusCode}.");
                }
                var mediaType = resp.Content.Headers.ContentType?.MediaType;
                if (mediaType is null || !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    return Fail("fetch_failed", "The URL did not return an HTML page.");
                }
                if (resp.Content.Headers.ContentLength is > MaxResponseBytes)
                {
                    return Fail("fetch_failed", "The page is too large to import.");
                }
                html = await ReadCappedAsync(resp, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                return Fail("fetch_failed", "Could not fetch the page.");
            }

            var parsed = JsonLdRecipeParser.Parse(html);
            return parsed is null
                ? Fail("no_recipe_found", "Could not find a recipe on this page.")
                : Result.Ok(parsed);
        }

        // ponytail: assumes UTF-8 — charset sniffing skipped; revisit if non-UTF-8 sites mis-parse.
        private static async Task<string> ReadCappedAsync(HttpResponseMessage resp, CancellationToken ct)
        {
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > MaxResponseBytes)
                {
                    throw new IOException("Response exceeded size cap.");
                }
                buffer.Write(chunk, 0, read);
            }
            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }

        private static Result<ImportedRecipe> Fail(string code, string message)
            => Result.Fail<ImportedRecipe>(new Error(message).WithMetadata("code", code));
    }
}
```

- [ ] **Step 5: Implement the DI extension**

Create `Application/Frigorino.Infrastructure/Services/RecipeImportDependencyInjection.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class RecipeImportDependencyInjection
    {
        // No config gate, no Null impl: the deterministic JSON-LD path has no vendor/API key — always on.
        public static IServiceCollection AddRecipeImport(this IServiceCollection services)
        {
            services.AddSingleton(RecipeImportService.CreateDefault());
            return services;
        }
    }
}
```

- [ ] **Step 6: Register in `Program.cs`**

In `Application/Frigorino.Web/Program.cs`, add the call next to the other AI/infra registrations (after `builder.Services.AddRecipeTagSuggestion(builder.Configuration);` at line ~99):

```csharp
builder.Services.AddRecipeImport();
```

(`AddRecipeImport` lives in `Frigorino.Infrastructure.Services`, already imported by the other `Add*` calls; if the build reports it missing, add `using Frigorino.Infrastructure.Services;` to the usings.)

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeImportServiceTests"`
Expected: PASS (4 cases). No external network occurs — blocked IPs throw in the connect callback.

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/RecipeImportConnect.cs Application/Frigorino.Infrastructure/Services/RecipeImportService.cs Application/Frigorino.Infrastructure/Services/RecipeImportDependencyInjection.cs Application/Frigorino.Web/Program.cs Application/Frigorino.Test/Infrastructure/RecipeImportServiceTests.cs
git commit -m "feat(recipes): SSRF-hardened recipe import fetch service"
```

---

### Task 4: `ImportRecipe` slice + route registration + client regen

**Files:**
- Create: `Application/Frigorino.Features/Recipes/ImportRecipe.cs`
- Modify: `Application/Frigorino.Web/Program.cs` (map `MapImportRecipe`)
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/openapi.json` + `src/lib/api/**` via `npm run api`

**Interfaces:**
- Consumes: `RecipeImportService.ImportAsync` (Task 3); `Recipe.Create/AddSection/AddItem/AddLink`; `ItemTextRouter.Analyze`; `IRecipeQuantityExtractionTrigger.OnItemRouted`; `db.FindActiveMembershipWithUserAsync`; `RecipeResponse.From`.
- Produces: endpoint `POST /api/household/{householdId:int}/recipes/import` → `201 RecipeResponse` | `400` | `404` | `422 {code}`. Generated TS: `importRecipeMutation`.

- [ ] **Step 1: Write the slice**

Create `Application/Frigorino.Features/Recipes/ImportRecipe.cs`:

```csharp
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Recipes
{
    public sealed record ImportRecipeRequest(string Url);

    public static class ImportRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapImportRecipe(this IEndpointRouteBuilder app)
        {
            app.MapPost("import", Handle)
               .WithName("ImportRecipe")
               .Produces<RecipeResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<RecipeResponse>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
            int householdId,
            ImportRecipeRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            RecipeImportService importService,
            IRecipeQuantityExtractionTrigger quantityTrigger,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipWithUserAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }
            var creator = membership.User;

            var import = await importService.ImportAsync(request.Url, ct);
            if (import.IsFailed)
            {
                var code = import.Errors[0].Metadata.TryGetValue("code", out var c) ? c?.ToString() : null;
                if (code == "invalid_url")
                {
                    return new Error("Enter a valid http(s) URL.").WithProperty("Url").ToValidationProblemResult();
                }
                return TypedResults.Problem(
                    detail: import.Errors[0].Message,
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { ["code"] = code });
            }

            var imported = import.Value;
            var creation = Recipe.Create(imported.Name, imported.Description, householdId, currentUser.UserId, imported.Servings);
            if (creation.IsFailed)
            {
                return creation.ToValidationProblem();
            }

            var recipe = creation.Value;
            recipe.CreatedByUser = creator;
            recipe.AddSection(null, null); // every recipe starts with one unnamed default section
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync(ct); // recipe + section now have real ids so AddItem can link the FK

            var section = recipe.Sections.First(s => s.IsActive);
            var routed = new List<(RecipeItem Item, ItemTextAnalysis Analysis)>();
            foreach (var ingredient in imported.Ingredients)
            {
                var analysis = ItemTextRouter.Analyze(ingredient);
                var add = recipe.AddItem(section.Id, analysis.CleanName, quantity: null, comment: null);
                if (add.IsSuccess)
                {
                    routed.Add((add.Value, analysis));
                }
            }
            recipe.AddLink(request.Url, imported.SourceName);
            await db.SaveChangesAsync(ct);

            // Bulk add carries the same obligation as the item-create slice: route each new item so
            // quantity extraction runs. Recipe items never chain product classification (MVP).
            foreach (var (item, analysis) in routed)
            {
                quantityTrigger.OnItemRouted(householdId, recipe.Id, item.Id, analysis);
            }

            return TypedResults.Created(
                $"/api/household/{householdId}/recipes/{recipe.Id}",
                RecipeResponse.From(recipe, creator, routed.Count));
        }
    }
}
```

- [ ] **Step 2: Map the endpoint in `Program.cs`**

In `Application/Frigorino.Web/Program.cs`, add to the `recipes` group (after `recipes.MapCreateRecipe();`, ~line 437):

```csharp
recipes.MapImportRecipe();
```

- [ ] **Step 3: Build the backend to verify it compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded. (This also re-emits `ClientApp/src/lib/openapi.json` with the new endpoint.)

- [ ] **Step 4: Regenerate the TypeScript client**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run api`
Expected: `src/lib/api/**` regenerated; `importRecipeMutation` now exists in `src/lib/api/@tanstack/react-query.gen.ts`.

Verify: `grep -r "importRecipeMutation" Application/Frigorino.Web/ClientApp/src/lib/api` returns a match.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Features/Recipes/ImportRecipe.cs Application/Frigorino.Web/Program.cs Application/Frigorino.Web/ClientApp/src/lib/openapi.json Application/Frigorino.Web/ClientApp/src/lib/api
git commit -m "feat(recipes): import recipe slice (POST /recipes/import)"
```

---

### Task 5: API integration test (stubbed fetcher)

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Infrastructure/StubRecipeImportService.cs`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs` (register stub)
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs` (add `TryImportRecipeAsync`)
- Create: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImport.Api.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportApiSteps.cs`

**Interfaces:**
- Consumes: `RecipeImportService` (override target), `ScenarioContextHolder`, `TestApiClient`.
- Produces: stub keyed by URL substring (`norecipe` → `no_recipe_found`; else success); `TestApiClient.TryImportRecipeAsync(string url)`.

- [ ] **Step 1: Write the stub**

The real `RecipeImportService` would hit the network; replace it with a deterministic subclass (uses the protected ctor). A URL containing `norecipe` yields the failure path; anything else yields a fixed recipe whose ingredients include `"20 apples"` so the existing extraction stub also runs.

Create `Application/Frigorino.IntegrationTests/Infrastructure/StubRecipeImportService.cs`:

```csharp
using FluentResults;
using Frigorino.Infrastructure.Services;

namespace Frigorino.IntegrationTests.Infrastructure;

// Network-free recipe import. URL containing "norecipe" → no_recipe_found; otherwise a fixed recipe.
public sealed class StubRecipeImportService : RecipeImportService
{
    public override Task<Result<ImportedRecipe>> ImportAsync(string url, CancellationToken ct)
    {
        if (url.Contains("norecipe", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result.Fail<ImportedRecipe>(
                new Error("Could not find a recipe on this page.").WithMetadata("code", "no_recipe_found")));
        }

        var imported = new ImportedRecipe(
            Name: "Imported Pancakes",
            Description: "From a URL",
            Servings: 4,
            Ingredients: new[] { "200g flour", "20 apples" },
            SourceName: "Example Blog");
        return Task.FromResult(Result.Ok(imported));
    }
}
```

- [ ] **Step 2: Register the stub in the test host**

In `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs`, inside `builder.ConfigureServices(...)` (after the `StubRecipeTagSuggester` block, ~line 86), add:

```csharp
            // Replace the real (network-hitting) recipe importer with a deterministic stub. Registered
            // as a concrete type (no interface), so RemoveAll + re-add the concrete service.
            services.RemoveAll<RecipeImportService>();
            services.AddSingleton<RecipeImportService>(new StubRecipeImportService());
```

(`RecipeImportService` is in `Frigorino.Infrastructure.Services`, already imported at the top of the file via `using Frigorino.Infrastructure.Services;`. `StubRecipeImportService` is in `Frigorino.IntegrationTests.Infrastructure`, the file's own namespace.)

- [ ] **Step 3: Add the API client method**

In `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`, add a method alongside the other recipe helpers (mirror the existing `PostAsync`/raw-response style; the raw-response variant returns `IAPIResponse`):

```csharp
    public Task<IAPIResponse> TryImportRecipeAsync(string url)
    {
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{ctx.HouseholdId}/recipes/import",
            new APIRequestContextOptions
            {
                Headers = AuthHeaders,
                DataObject = new { url },
            });
    }
```

(If `AuthHeaders` / `DataObject` differ from the file's convention, mirror the nearest existing `Try*Async` raw-response method — e.g. `TryCreateListItemAsync` — exactly.)

- [ ] **Step 4: Write the feature**

Create `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImport.Api.feature`:

```gherkin
Feature: Recipe import API

  Background:
    Given I am logged in with an active household

  Scenario: Importing a URL creates a recipe with ingredients and a source link
    When I import the recipe URL "https://example.com/pancakes" via the API
    Then the API response status is 201
    And the imported recipe has 2 ingredients
    And the imported recipe has 1 source link

  Scenario: Importing the created recipe extracts quantities without classifying products
    When I import the recipe URL "https://example.com/pancakes" via the API
    Then the API response status is 201
    And the imported recipe item eventually has text "apples" with quantity 20
    And the Products table is empty

  Scenario: Importing a page with no recipe returns 422 with a no_recipe_found code
    When I import the recipe URL "https://example.com/norecipe" via the API
    Then the API response status is 422
    And the API response has the import error code "no_recipe_found"

  Scenario: Importing an invalid URL returns a validation error
    When I import the recipe URL "not-a-url" via the API
    Then the API response status is 400
    And the API response has a validation error for "Url"
```

(The `Products table is empty` and `validation error for "Url"` Then-steps already exist in the recipe/products IT bindings — reuse them verbatim. Confirm the exact phrasing by grepping the Steps folder before writing; if the products-empty step text differs, match it.)

- [ ] **Step 5: Write the steps**

Create `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportApiSteps.cs`. Step phrases must be globally unique across the IT assembly.

```csharp
using System.Text.Json;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeImportApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    private int _importedRecipeId;

    [When("I import the recipe URL {string} via the API")]
    public async Task WhenIImportTheRecipeUrlViaTheApi(string url)
    {
        ctx.LastApiResponse = await api.TryImportRecipeAsync(url);
        if (ctx.LastApiResponse.Status == 201)
        {
            var json = (await ctx.LastApiResponse.JsonAsync())!.Value;
            _importedRecipeId = json.GetProperty("id").GetInt32();
        }
    }

    [Then("the imported recipe has {int} ingredients")]
    public async Task ThenTheImportedRecipeHasIngredients(int count)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actual = await db.RecipeItems.CountAsync(i => i.RecipeId == _importedRecipeId && i.IsActive);
        Assert.Equal(count, actual);
    }

    [Then("the imported recipe has {int} source link")]
    [Then("the imported recipe has {int} source links")]
    public async Task ThenTheImportedRecipeHasSourceLinks(int count)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actual = await db.RecipeLinks.CountAsync(l => l.RecipeId == _importedRecipeId && l.IsActive);
        Assert.Equal(count, actual);
    }

    [Then("the imported recipe item eventually has text {string} with quantity {int}")]
    public async Task ThenTheImportedRecipeItemEventuallyHasText(string text, int quantity)
    {
        // The extraction job runs async on the queue; poll briefly (mirrors the existing recipe
        // extraction Then-step's eventual-consistency pattern).
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var scope = ctx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.RecipeItems.FirstOrDefaultAsync(
                i => i.RecipeId == _importedRecipeId && i.Text == text && i.QuantityValue == quantity);
            if (item is not null)
            {
                return;
            }
            await Task.Delay(100);
        }
        throw new Xunit.Sdk.XunitException($"Recipe item '{text}' with quantity {quantity} did not appear.");
    }

    [Then("the API response has the import error code {string}")]
    public async Task ThenTheApiResponseHasTheImportErrorCode(string code)
    {
        var json = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        Assert.Equal(code, json.GetProperty("code").GetString());
    }
}
```

(If `db.RecipeItems` / `db.RecipeLinks` `DbSet` names differ, confirm via `ApplicationDbContext`. The "quantity 20" maps to the `StubQuantityExtractor`, which parses a leading number; "20 apples" → name "apples", value 20.)

- [ ] **Step 6: Build the SPA, then run the API integration tests**

The IT harness serves `ClientApp/build`; build it once so the host boots:

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run build`
Then run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~RecipeImportApi"`
Expected: PASS (4 scenarios). Confirm the run reports the expected scenario count (don't trust a green from zero matched).

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Infrastructure/StubRecipeImportService.cs Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImport.Api.feature Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportApiSteps.cs
git commit -m "test(recipes): API integration tests for recipe import"
```

---

# Phase 2 — Frontend

### Task 6: `useImportRecipe` hook + i18n keys

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/useImportRecipe.ts`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

**Interfaces:**
- Consumes: generated `importRecipeMutation`, `getRecipesQueryKey` (Task 4).
- Produces: `useImportRecipe()` mutation hook (caller passes `{ path: { householdId }, body: { url } }`); i18n keys under `recipes.import.*`.

- [ ] **Step 1: Write the hook**

Create `Application/Frigorino.Web/ClientApp/src/features/recipes/useImportRecipe.ts`:

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    importRecipeMutation,
    getRecipesQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useImportRecipe = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...importRecipeMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipesQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
```

- [ ] **Step 2: Add English i18n keys**

In `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`, add inside the existing `"recipes": { ... }` object:

```json
"import": {
  "open": "Import from URL",
  "title": "Import a recipe",
  "urlLabel": "Recipe URL",
  "urlPlaceholder": "https://example.com/best-pancakes",
  "submit": "Import",
  "importing": "Importing…",
  "success": "Recipe imported — review and save",
  "invalidUrl": "Enter a valid recipe URL (http or https).",
  "fetchFailed": "Couldn't reach that page. Check the link or add the recipe manually.",
  "noRecipeFound": "Couldn't read a recipe from that page. Add it manually instead."
}
```

- [ ] **Step 3: Add German i18n keys**

In `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`, add inside the existing `"recipes": { ... }` object:

```json
"import": {
  "open": "Aus URL importieren",
  "title": "Rezept importieren",
  "urlLabel": "Rezept-URL",
  "urlPlaceholder": "https://example.com/beste-pfannkuchen",
  "submit": "Importieren",
  "importing": "Wird importiert…",
  "success": "Rezept importiert – prüfen und speichern",
  "invalidUrl": "Gib eine gültige Rezept-URL ein (http oder https).",
  "fetchFailed": "Seite nicht erreichbar. Prüfe den Link oder füge das Rezept manuell hinzu.",
  "noRecipeFound": "Auf dieser Seite wurde kein Rezept gefunden. Bitte manuell hinzufügen."
}
```

- [ ] **Step 4: Type-check**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc`
Expected: no errors (the hook resolves `importRecipeMutation`; the keys type-check against the JSON).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/useImportRecipe.ts Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(recipes): useImportRecipe hook + i18n"
```

---

### Task 7: Import dialog + overview entry point

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/ImportRecipeSheet.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipesPage.tsx`

**Interfaces:**
- Consumes: `useImportRecipe` (Task 6), `useNavigate`, `toast` (sonner).
- Produces: `<ImportRecipeSheet open onClose householdId />`; an "Import from URL" direct action on the recipes overview.

- [ ] **Step 1: Write the dialog component**

Create `Application/Frigorino.Web/ClientApp/src/features/recipes/components/ImportRecipeSheet.tsx`:

```tsx
import {
    Alert,
    Button,
    CircularProgress,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    TextField,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useImportRecipe } from "../useImportRecipe";

interface ImportRecipeSheetProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
}

export const ImportRecipeSheet = ({
    open,
    onClose,
    householdId,
}: ImportRecipeSheetProps) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const importRecipe = useImportRecipe();
    const [url, setUrl] = useState("");

    const messageFor = (error: unknown): string => {
        const code = (error as { code?: string } | null)?.code;
        if (code === "no_recipe_found") {
            return t("recipes.import.noRecipeFound");
        }
        if (code === "fetch_failed") {
            return t("recipes.import.fetchFailed");
        }
        // 400 ValidationProblem (invalid_url) has an { errors: { Url: [...] } } body and no code.
        const errors = (error as { errors?: Record<string, string[]> } | null)
            ?.errors;
        if (errors && Object.keys(errors).length > 0) {
            return t("recipes.import.invalidUrl");
        }
        return t("common.errorOccurred");
    };

    const handleSubmit = async () => {
        try {
            const recipe = await importRecipe.mutateAsync({
                path: { householdId },
                body: { url: url.trim() },
            });
            toast.success(t("recipes.import.success"));
            onClose();
            setUrl("");
            navigate({
                to: "/recipes/$recipeId/edit",
                params: { recipeId: String(recipe.id) },
            });
        } catch {
            // Error is surfaced inline via importRecipe.error below.
        }
    };

    return (
        <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
            <DialogTitle>{t("recipes.import.title")}</DialogTitle>
            <DialogContent>
                <TextField
                    autoFocus
                    fullWidth
                    type="url"
                    label={t("recipes.import.urlLabel")}
                    placeholder={t("recipes.import.urlPlaceholder")}
                    value={url}
                    onChange={(e) => setUrl(e.target.value)}
                    slotProps={{
                        htmlInput: { "data-testid": "recipe-import-url" },
                    }}
                    sx={{ mt: 1 }}
                />
                {importRecipe.isError && (
                    <Alert
                        severity="error"
                        sx={{ mt: 2 }}
                        data-testid="recipe-import-error"
                    >
                        {messageFor(importRecipe.error)}
                    </Alert>
                )}
            </DialogContent>
            <DialogActions>
                <Button onClick={onClose}>{t("common.cancel")}</Button>
                <Button
                    variant="contained"
                    onClick={handleSubmit}
                    disabled={!url.trim() || importRecipe.isPending}
                    startIcon={
                        importRecipe.isPending ? (
                            <CircularProgress size={16} />
                        ) : undefined
                    }
                    data-testid="recipe-import-submit"
                >
                    {importRecipe.isPending
                        ? t("recipes.import.importing")
                        : t("recipes.import.submit")}
                </Button>
            </DialogActions>
        </Dialog>
    );
};
```

(Confirm `common.cancel` and `common.errorOccurred` exist in the locale files — they're used across the app; if `common.cancel` is absent, use the nearest existing cancel key.)

- [ ] **Step 2: Wire the entry point into `RecipesPage`**

In `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipesPage.tsx`:

Add the icon + component imports:

```tsx
import { Add, Download, Search } from "@mui/icons-material";
```

```tsx
import { ImportRecipeSheet } from "../components/ImportRecipeSheet";
```

Add dialog state near the other `useState` hooks (after `const [importOpen, setImportOpen] = ...`):

```tsx
    const [importOpen, setImportOpen] = useState(false);
```

Add the import direct action to the head bar (alongside the existing create `Add` action), and render the sheet. Replace the existing `directActions` array on `PageHeadActionBar`:

```tsx
                directActions={[
                    {
                        icon: <Download />,
                        onClick: () => setImportOpen(true),
                    },
                    { icon: <Add />, onClick: handleCreateRecipe },
                ]}
```

Render the sheet inside the page (e.g. just before the closing `</Container>` or after `</Container>`, inside the fragment):

```tsx
                <ImportRecipeSheet
                    open={importOpen}
                    onClose={() => setImportOpen(false)}
                    householdId={householdId}
                />
```

Also add a testid to the import action so the UI IT can open it. The simplest reliable hook: give the head-bar import button a testid. If `HeadNavigationAction` does not support a testid prop, add an optional `testid?: string` to the action interface in `components/shared/PageHeadActionBar.tsx` and spread it onto the rendered `IconButton` as `data-testid`. Then set `testid: "recipe-import-open"` on the import action. (Check `PageHeadActionBar.tsx` first; only extend the interface if no testid path already exists.)

- [ ] **Step 3: Type-check + lint + format**

Run (from `Application/Frigorino.Web/ClientApp/`):
```
npm run tsc
npm run lint
npm run prettier
```
Expected: all clean.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/ImportRecipeSheet.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipesPage.tsx Application/Frigorino.Web/ClientApp/src/components/shared/PageHeadActionBar.tsx
git commit -m "feat(recipes): import-from-URL dialog on recipes overview"
```

---

# Phase 3 — UI integration test

### Task 8: UI integration test (drive the SPA)

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImport.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportUiSteps.cs`

**Interfaces:**
- Consumes: the stub from Task 5 (already registered in the test host), the testids `recipe-import-open`, `recipe-import-url`, `recipe-import-submit`, `recipe-import-error`, and the edit page's existing testids.
- Produces: UI coverage of the happy-path redirect and the inline error.

- [ ] **Step 1: Identify a stable edit-page testid**

Open `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx` and pick an existing stable testid that proves the edit page rendered (e.g. the title/description input `recipe-description-input` mentioned in `knowledge/Recipes.md`). Use that in the Then-step. If none is suitable, assert on the URL containing `/edit` via `ctx.Page.WaitForURLAsync("**/edit")`.

- [ ] **Step 2: Write the feature**

Create `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImport.feature`:

```gherkin
Feature: Recipe import UI

  Background:
    Given I am logged in with an active household

  Scenario: Importing a URL lands on the edit page
    Given I am on the recipes page
    When I open the import dialog
    And I submit the import URL "https://example.com/pancakes"
    Then I am taken to the recipe edit page

  Scenario: A page with no recipe shows an inline error
    Given I am on the recipes page
    When I open the import dialog
    And I submit the import URL "https://example.com/norecipe"
    Then the import dialog shows an error
```

- [ ] **Step 3: Write the steps**

Create `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportUiSteps.cs`. Use retrying `Expect(...)` assertions; assert on testids only. Step text must be globally unique.

```csharp
using Microsoft.Playwright;

namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeImportUiSteps(ScenarioContextHolder ctx)
{
    [Given("I am on the recipes page")]
    public async Task GivenIAmOnTheRecipesPage()
    {
        await ctx.Page.GotoAsync($"{ctx.Factory.BaseAddress}/recipes");
        await ctx.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    [When("I open the import dialog")]
    public async Task WhenIOpenTheImportDialog()
    {
        await ctx.Page.GetByTestId("recipe-import-open").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-import-url")).ToBeVisibleAsync();
    }

    [When("I submit the import URL {string}")]
    public async Task WhenISubmitTheImportUrl(string url)
    {
        await ctx.Page.GetByTestId("recipe-import-url").FillAsync(url);
        await ctx.Page.GetByTestId("recipe-import-submit").ClickAsync();
    }

    [Then("I am taken to the recipe edit page")]
    public async Task ThenIAmTakenToTheRecipeEditPage()
    {
        await ctx.Page.WaitForURLAsync("**/recipes/*/edit");
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-description-input")).ToBeVisibleAsync();
    }

    [Then("the import dialog shows an error")]
    public async Task ThenTheImportDialogShowsAnError()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-import-error")).ToBeVisibleAsync();
    }
}
```

(If `recipe-description-input` is not present on the edit page, drop that assertion and keep the `WaitForURLAsync`. If `GetByTestId` is not the helper used elsewhere, mirror the existing recipe UI steps' locator style, e.g. `Locator("[data-testid=...]")`.)

- [ ] **Step 4: Build the SPA, then run the UI integration tests**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run build`  (picks up the new testids)
Then run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~RecipeImportUi"`
Expected: PASS (2 scenarios). Confirm the scenario count matches.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImport.feature Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportUiSteps.cs
git commit -m "test(recipes): UI integration tests for recipe import"
```

---

# Phase 4 — Verification & finish

### Task 9: Full verification gate

**Files:** none (verification only).

- [ ] **Step 1: Frontend checks**

Run (from `Application/Frigorino.Web/ClientApp/`):
```
npm run tsc
npm run lint
npm run prettier
npm run build
```
Expected: all clean; build emits to `ClientApp/build`.

- [ ] **Step 2: Full solution tests**

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS — `Frigorino.Test` (incl. the 3 new test classes) + `Frigorino.IntegrationTests` (incl. the new API + UI scenarios). Read the pass/fail summary lines; don't trust a tail exit code through a pipe.

- [ ] **Step 3: Docker build**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: build succeeds (catches Dockerfile/SPA/pipeline drift). No Dockerfile change is expected (no new project).

- [ ] **Step 4: Finish the branch**

Use the `superpowers:finishing-a-development-branch` skill to decide merge/PR. The branch `feat/recipe-url-import` already carries the spec + IDEAS edits commit. Mention this is UAT-bound `stage` work (stage→main FF promotion).

---

## Self-Review

**Spec coverage:**
- Infrastructure `RecipeImportService` (fetch + SSRF + parse) → Tasks 1–3. ✓
- `AddRecipeImport` DI (no config gate) → Task 3. ✓
- `ImportRecipe` slice (Create/AddSection/AddItem/AddLink + extraction trigger) → Task 4. ✓
- Frontend hook + dialog + entry point + navigation → Tasks 6–7. ✓
- i18n en+de under existing namespace → Task 6. ✓
- Regenerated TS client → Task 4. ✓
- Unit tests (parser + IP classifier) → Tasks 1–2; service guard test → Task 3. ✓
- Integration test (stubbed fetcher) → Task 5 (API) + Task 8 (UI). ✓
- Verification gate → Task 9. ✓
- Error contract `invalid_url`/`fetch_failed`/`no_recipe_found` consistent across service (Task 3) → slice (Task 4) → frontend mapping (Task 7) → i18n keys (Task 6) → IT assertions (Task 5). ✓
- Caps (3 MB / 5 redirects / 10s / 100 ingredients / 1000-char description) → Tasks 1 + 3. ✓

**Known coverage gap (stated, not silent):** the *real* HTTP fetch + parse success path is not integration-tested (the IT stubs the service to avoid network + the SSRF self-block). It is covered by the parser unit tests (Task 1) and the guard tests (Tasks 2–3, which exercise the real client against blocked IPs with no network). This matches the spec's testing decision.

**Type consistency:** `ImportedRecipe(Name, Description, Servings, Ingredients, SourceName)` is identical in Tasks 1, 3, 5. `RecipeImportService.ImportAsync` signature + `code` metadata keys identical in Tasks 3, 4, 5. `importRecipeMutation` produced in Task 4, consumed in Task 6. Testids (`recipe-import-open|url|submit|error`) produced in Task 7, consumed in Task 8.

**Placeholder scan:** No TBD/TODO. A few steps ask the implementer to confirm an exact existing name (DbSet names, `common.cancel`, a head-bar testid path, the edit-page testid) before mirroring — these are verification instructions with a concrete fallback, not unresolved gaps.
