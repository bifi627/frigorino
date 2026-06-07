# Fractional-index ordering (`SortOrder` int → `Rank` string) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the integer midpoint `SortOrder` scheme (which silently collapses after ~13 reorders into one slot, with no recovery path) with a server-minted lexicographic string `Rank` (fractional indexing) that never exhausts and merges concurrent reorders cleanly — the ordering primitive the committed real-time-sync roadmap needs.

**Architecture:** A pure in-house `FractionalIndex` algorithm in Domain mints opaque string keys server-side. The `List`/`Inventory` aggregates compute keys via `GenerateKeyBetween(prev, next)` on append/reorder/status-toggle. Reads order by the string `Rank` (+ `Id` tiebreaker). A partial unique index `WHERE IsActive` guards against concurrent same-slot collisions; minting handlers retry on the `23505` unique violation. The client stops computing any keys — `common/sortOrder.ts` is deleted, optimistic updates splice the cached array, and rendering trusts server order. Migration is expand-only: add `rank`, backfill from `sort_order` at startup, leave `sort_order` as a dead column to drop later.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql 10.0.2 (Postgres), FluentResults, xUnit + FakeItEasy + EF InMemory, Reqnroll + Playwright, React 19 + TanStack Query + dnd-kit, hey-api codegen.

**Spec:** `docs/superpowers/specs/2026-06-07-fractional-index-ordering-design.md`

---

## File Structure

**Create:**
- `Application/Frigorino.Domain/Entities/FractionalIndex.cs` — pure key algorithm (replaces `SortOrderCalculator`).
- `Application/Frigorino.Test/Domain/FractionalIndexTests.cs` — reference-vector unit tests.
- `Application/Frigorino.Features/Items/RankRetry.cs` — shared unique-violation retry helper.
- `Application/Frigorino.Infrastructure/Services/RankBackfill.cs` — one-time startup backfill.
- One EF migration under `Application/Frigorino.Infrastructure/Migrations/` (generated).

**Modify (backend):**
- `Domain/Entities/ListItem.cs`, `InventoryItem.cs` — add `Rank`, keep `SortOrder` (dead).
- `Domain/Entities/List.cs`, `Inventory.cs` — rewrite append/reorder to keys; delete `CompactItems`.
- `Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs`, `InventoryItemConfiguration.cs` — map `Rank`, add partial unique index.
- `Features/Lists/Items/*.cs` and `Features/Inventories/Items/*.cs` — wrap minting handlers in retry; delete compact slices.
- `Features/Lists/Items/ListItemResponse.cs`, `Features/Inventories/Items/InventoryItemResponse.cs` — `SortOrder` → `Rank`.
- `Features/Lists/Items/GetItems.cs`, `Features/Inventories/Items/GetInventoryItems.cs` — order by `Rank`, `Id`.
- `Web/Program.cs` — call backfill after migrate; drop compact wiring.

**Modify (frontend):**
- Delete `src/common/sortOrder.ts`.
- `src/features/lists/items/useReorderListItem.ts`, `useToggleListItemStatus.ts`, `useCreateListItem.ts`.
- `src/features/inventories/items/useReorderInventoryItem.ts`, `useCreateInventoryItem.ts`.
- `src/components/sortables/SortableList.tsx` — custom mode preserves array order.
- Delete `src/features/lists/items/useCompactListItems.ts`, `src/features/inventories/items/useCompactInventoryItems.ts`.
- Regenerated `src/lib/api/*` via `npm run api`.

**Delete:** `Domain/Entities/SortOrderCalculator.cs`, `Features/Lists/Items/CompactItems.cs`, `Features/Inventories/Items/CompactInventoryItems.cs`.

---

## Task 1: FractionalIndex algorithm (Domain) — TDD

Pure port of the canonical Figma/rocicorp algorithm (alphabet `0-9A-Za-z`, integer-length-prefixed format). This is the crux — build it test-first against the published vectors.

**Files:**
- Create: `Application/Frigorino.Domain/Entities/FractionalIndex.cs`
- Test: `Application/Frigorino.Test/Domain/FractionalIndexTests.cs`

- [ ] **Step 1: Write the failing tests** (`FractionalIndexTests.cs`)

```csharp
using Frigorino.Domain.Entities;
using Xunit;

namespace Frigorino.Test.Domain
{
    public class FractionalIndexTests
    {
        // Reference vectors from the canonical fractional-indexing spec.
        [Theory]
        [InlineData(null, null, "a0")]
        [InlineData("a0", null, "a1")]
        [InlineData("a1", null, "a2")]
        [InlineData(null, "a0", "Zz")]
        [InlineData(null, "Zz", "Zy")]
        [InlineData("a0", "a1", "a0V")]
        [InlineData("a0V", "a1", "a0l")]
        [InlineData("a0", "a0V", "a0G")]
        [InlineData("Zz", "a0", "ZzV")]
        [InlineData("Zz", null, "a0")]
        public void GenerateKeyBetween_MatchesReferenceVectors(string? a, string? b, string expected)
        {
            Assert.Equal(expected, FractionalIndex.GenerateKeyBetween(a, b));
        }

        [Fact]
        public void GenerateKeyBetween_ResultSortsStrictlyBetween()
        {
            var mid = FractionalIndex.GenerateKeyBetween("a0", "a1");
            Assert.True(string.CompareOrdinal("a0", mid) < 0);
            Assert.True(string.CompareOrdinal(mid, "a1") < 0);
        }

        [Fact]
        public void GenerateKeyBetween_ThrowsWhenOutOfOrder()
        {
            Assert.Throws<ArgumentException>(() => FractionalIndex.GenerateKeyBetween("a1", "a0"));
        }

        [Fact]
        public void GenerateKeyBetween_ThirteenDropsIntoSameSlot_ProducesDistinctKeys()
        {
            // The bug this whole change fixes: repeatedly inserting between "a0" and a moving
            // upper bound never collides — keys just get longer.
            var lower = "a0";
            var upper = "a1";
            var seen = new HashSet<string> { lower, upper };
            for (var i = 0; i < 50; i++)
            {
                var mid = FractionalIndex.GenerateKeyBetween(lower, upper);
                Assert.True(string.CompareOrdinal(lower, mid) < 0 && string.CompareOrdinal(mid, upper) < 0);
                Assert.True(seen.Add(mid), $"collision at iteration {i}: {mid}");
                upper = mid; // keep dropping into the same shrinking slot
            }
        }

        [Theory]
        [InlineData(0, new string[0])]
        [InlineData(1, new[] { "a0" })]
        [InlineData(2, new[] { "a0", "a1" })]
        [InlineData(3, new[] { "a0", "a1", "a2" })]
        public void GenerateKeysBetween_NullNull_AppendsSequentially(int n, string[] expected)
        {
            Assert.Equal(expected, FractionalIndex.GenerateKeysBetween(null, null, n).ToArray());
        }

        [Fact]
        public void GenerateKeysBetween_ResultsAreStrictlyIncreasing()
        {
            var keys = FractionalIndex.GenerateKeysBetween("a0", "a1", 5).ToArray();
            Assert.Equal(5, keys.Length);
            for (var i = 1; i < keys.Length; i++)
            {
                Assert.True(string.CompareOrdinal(keys[i - 1], keys[i]) < 0);
            }
            Assert.True(string.CompareOrdinal("a0", keys[0]) < 0);
            Assert.True(string.CompareOrdinal(keys[^1], "a1") < 0);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~FractionalIndexTests"`
Expected: FAIL — `FractionalIndex` does not exist (compile error).

- [ ] **Step 3: Implement `FractionalIndex.cs`** (faithful port — keep the structure 1:1 with the reference so the vectors pin correctness)

```csharp
using System.Text;

namespace Frigorino.Domain.Entities
{
    // Canonical fractional-indexing algorithm (Figma/rocicorp "Implementing Fractional Indexing").
    // Mints opaque lexicographic string keys with unbounded precision: a key can always be
    // generated strictly between any two distinct keys, so ordering never exhausts and a reorder
    // is always a single-row write. Keys are compared with ordinal (byte) string comparison —
    // the DB column MUST use the C collation to match (see ListItemConfiguration).
    //
    // Replaces the old integer SortOrderCalculator. Pure: no DbContext, no I/O.
    public static class FractionalIndex
    {
        private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        // Smallest/largest integer heads bound the integer-part length encoding.
        private const string SmallestInteger = "A00000000000000000000000000";

        public static string GenerateKeyBetween(string? a, string? b)
        {
            if (a is not null)
            {
                ValidateOrderKey(a);
            }
            if (b is not null)
            {
                ValidateOrderKey(b);
            }
            if (a is not null && b is not null && string.CompareOrdinal(a, b) >= 0)
            {
                throw new ArgumentException($"Order key '{a}' is not less than '{b}'.");
            }

            if (a is null)
            {
                if (b is null)
                {
                    return "a" + Digits[0]; // "a0"
                }
                var ib0 = GetIntegerPart(b);
                var fb0 = b[ib0.Length..];
                if (ib0 == SmallestInteger)
                {
                    return ib0 + Midpoint("", fb0);
                }
                if (string.CompareOrdinal(ib0, b) < 0)
                {
                    return ib0;
                }
                var dec = DecrementInteger(ib0)
                    ?? throw new InvalidOperationException("Cannot generate key before the smallest possible key.");
                return dec;
            }

            if (b is null)
            {
                var ia0 = GetIntegerPart(a);
                var fa0 = a[ia0.Length..];
                var inc = IncrementInteger(ia0);
                return inc is null ? ia0 + Midpoint(fa0, null) : inc;
            }

            var ia = GetIntegerPart(a);
            var fa = a[ia.Length..];
            var ib = GetIntegerPart(b);
            var fb = b[ib.Length..];
            if (ia == ib)
            {
                return ia + Midpoint(fa, fb);
            }
            var i = IncrementInteger(ia)
                ?? throw new InvalidOperationException("Cannot increment integer part.");
            return string.CompareOrdinal(i, b) < 0 ? i : ia + Midpoint(fa, null);
        }

        // n evenly distributed keys strictly between a and b (used by the backfill seed).
        public static IReadOnlyList<string> GenerateKeysBetween(string? a, string? b, int n)
        {
            if (n < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }
            if (n == 0)
            {
                return Array.Empty<string>();
            }
            if (n == 1)
            {
                return new[] { GenerateKeyBetween(a, b) };
            }
            if (b is null)
            {
                var c = GenerateKeyBetween(a, null);
                var resultUp = new List<string> { c };
                for (var x = 0; x < n - 1; x++)
                {
                    c = GenerateKeyBetween(c, null);
                    resultUp.Add(c);
                }
                return resultUp;
            }
            if (a is null)
            {
                var c = GenerateKeyBetween(null, b);
                var resultDown = new List<string> { c };
                for (var x = 0; x < n - 1; x++)
                {
                    c = GenerateKeyBetween(null, c);
                    resultDown.Add(c);
                }
                resultDown.Reverse();
                return resultDown;
            }
            var mid = n / 2;
            var midKey = GenerateKeyBetween(a, b);
            var result = new List<string>();
            result.AddRange(GenerateKeysBetween(a, midKey, mid));
            result.Add(midKey);
            result.AddRange(GenerateKeysBetween(midKey, b, n - mid - 1));
            return result;
        }

        private static string Midpoint(string a, string? b)
        {
            if (b is not null && string.CompareOrdinal(a, b) >= 0)
            {
                throw new ArgumentException($"Midpoint: '{a}' >= '{b}'.");
            }
            if (a.Length > 0 && a[^1] == '0' || (b is not null && b.Length > 0 && b[^1] == '0'))
            {
                throw new ArgumentException("Trailing zero in fractional part.");
            }
            if (b is not null)
            {
                var n = 0;
                while ((n < a.Length ? a[n] : '0') == b[n])
                {
                    n++;
                }
                if (n > 0)
                {
                    return b[..n] + Midpoint(n < a.Length ? a[n..] : "", b[n..]);
                }
            }
            var digitA = a.Length > 0 ? Digits.IndexOf(a[0]) : 0;
            var digitB = b is not null ? Digits.IndexOf(b[0]) : Digits.Length;
            if (digitB - digitA > 1)
            {
                var midDigit = (int)Math.Round(0.5 * (digitA + digitB), MidpointRounding.AwayFromZero);
                return Digits[midDigit].ToString();
            }
            if (b is not null && b.Length > 1)
            {
                return b[..1];
            }
            return Digits[digitA] + Midpoint(a.Length > 0 ? a[1..] : "", null);
        }

        private static int GetIntegerLength(char head)
        {
            if (head is >= 'a' and <= 'z')
            {
                return head - 'a' + 2;
            }
            if (head is >= 'A' and <= 'Z')
            {
                return 'Z' - head + 2;
            }
            throw new ArgumentException($"Invalid integer head '{head}'.");
        }

        private static string GetIntegerPart(string key)
        {
            var len = GetIntegerLength(key[0]);
            if (len > key.Length)
            {
                throw new ArgumentException($"Invalid order key '{key}'.");
            }
            return key[..len];
        }

        private static void ValidateOrderKey(string key)
        {
            if (key == SmallestInteger)
            {
                throw new ArgumentException($"Invalid order key '{key}'.");
            }
            var i = GetIntegerPart(key);
            var f = key[i.Length..];
            if (f.Length > 0 && f[^1] == '0')
            {
                throw new ArgumentException($"Invalid order key '{key}' (trailing zero).");
            }
        }

        private static string? IncrementInteger(string x)
        {
            var head = x[0];
            var digs = x[1..].Select(c => Digits.IndexOf(c)).ToList();
            var carry = true;
            for (var i = digs.Count - 1; carry && i >= 0; i--)
            {
                var d = digs[i] + 1;
                if (d == Digits.Length)
                {
                    digs[i] = 0;
                }
                else
                {
                    digs[i] = d;
                    carry = false;
                }
            }
            if (carry)
            {
                if (head == 'Z')
                {
                    return "a" + Digits[0];
                }
                if (head == 'z')
                {
                    return null;
                }
                var h = (char)(head + 1);
                if (h > 'a')
                {
                    digs.Add(0);
                }
                else
                {
                    digs.RemoveAt(digs.Count - 1);
                }
                return h + new string(digs.Select(d => Digits[d]).ToArray());
            }
            return head + new string(digs.Select(d => Digits[d]).ToArray());
        }

        private static string? DecrementInteger(string x)
        {
            var head = x[0];
            var digs = x[1..].Select(c => Digits.IndexOf(c)).ToList();
            var borrow = true;
            for (var i = digs.Count - 1; borrow && i >= 0; i--)
            {
                var d = digs[i] - 1;
                if (d == -1)
                {
                    digs[i] = Digits.Length - 1;
                }
                else
                {
                    digs[i] = d;
                    borrow = false;
                }
            }
            if (borrow)
            {
                if (head == 'a')
                {
                    return "Z" + Digits[^1];
                }
                if (head == 'A')
                {
                    return null;
                }
                var h = (char)(head - 1);
                if (h < 'Z')
                {
                    digs.Add(Digits.Length - 1);
                }
                else
                {
                    digs.RemoveAt(digs.Count - 1);
                }
                return h + new string(digs.Select(d => Digits[d]).ToArray());
            }
            return head + new string(digs.Select(d => Digits[d]).ToArray());
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~FractionalIndexTests"`
Expected: PASS (all). If any vector fails, the port has a bug — fix `FractionalIndex.cs`, do NOT change the expected vectors (they are the spec).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/FractionalIndex.cs Application/Frigorino.Test/Domain/FractionalIndexTests.cs
git commit -m "feat: add FractionalIndex string-key algorithm (Domain)"
```

---

## Task 2: Add `Rank` to entities + EF config + migration (expand)

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/ListItem.cs`, `InventoryItem.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs`, `InventoryItemConfiguration.cs`
- Create (generated): migration under `Application/Frigorino.Infrastructure/Migrations/`

- [ ] **Step 1: Add `Rank` property, keep `SortOrder`**

In `ListItem.cs`, directly after the `public int SortOrder { get; set; }` line, add:

```csharp
        // Lexicographic ordering key (fractional index). Opaque, server-minted, never shown in UI.
        // Replaces SortOrder; SortOrder is retained as a dead column until the deferred cleanup.
        public string Rank { get; set; } = string.Empty;
```

Do the identical edit in `InventoryItem.cs` after its `public int SortOrder { get; set; }`.

- [ ] **Step 2: Map `Rank` + partial unique index in EF config**

In `ListItemConfiguration.cs`, after the `builder.Property(li => li.SortOrder).IsRequired();` block add:

```csharp
            builder.Property(li => li.Rank)
                .HasColumnType("text")
                .UseCollation("C") // byte-ordinal; matches FractionalIndex's ordinal comparison
                .IsRequired();
```

And in the index section, after the existing composite index, add:

```csharp
            // Ordered fetch + concurrent-reorder collision guard (active rows only).
            builder.HasIndex(li => new { li.ListId, li.Status, li.Rank })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("UX_ListItems_ListId_Status_Rank_Active");
```

In `InventoryItemConfiguration.cs`, after `builder.Property(ii => ii.SortOrder).IsRequired();` add the same `Rank` property block (substituting `ii`), and after the indexes add:

```csharp
            builder.HasIndex(ii => new { ii.InventoryId, ii.Rank })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("UX_InventoryItems_InventoryId_Rank_Active");
```

> Note: `Rank` is `IsRequired()` in the model but the *column* is added nullable by the migration (Step 3) so existing rows survive until the backfill fills them. EF's `IsRequired` only affects validation/SaveChanges of tracked entities, all of which set `Rank`. Leave it as written.

- [ ] **Step 3: Generate the migration**

Run:
```bash
dotnet ef migrations add AddItemRank --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```

- [ ] **Step 4: Edit the generated migration so the `rank` columns are nullable**

EF will emit `AddColumn<string>("Rank", ..., nullable: false, defaultValue: "")` because the property is required. Open the new `*_AddItemRank.cs` and change BOTH `Rank` `AddColumn` calls to be nullable with no default, so the backfill (Task 8) — not a bogus `""` default — populates them:

```csharp
            migrationBuilder.AddColumn<string>(
                name: "Rank",
                table: "ListItems",
                type: "text",
                nullable: true,
                collation: "C");
            // ...and the InventoryItems one likewise: nullable: true, collation: "C".
```

Leave the generated `CreateIndex` calls (the unique filtered indexes) as-is. Confirm the `Down` drops the indexes and columns.

- [ ] **Step 5: Build to verify the model + migration compile**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded. (Migrations apply automatically at next app start; no manual apply here.)

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Domain/Entities/ListItem.cs Application/Frigorino.Domain/Entities/InventoryItem.cs Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs Application/Frigorino.Infrastructure/EntityFramework/Configurations/InventoryItemConfiguration.cs Application/Frigorino.Infrastructure/Migrations/
git commit -m "feat: add nullable Rank column + partial unique index (expand)"
```

---

## Task 3: Rewrite `List` aggregate ordering to keys — TDD

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/List.cs` (`ComputeAppendSortOrder` → `ComputeAppendRank`, `ReorderItem`, delete `CompactItems`, callers in `AddItem`/`AddMediaItem`/`UpdateItem`/`ToggleItemStatus`/`RestoreItem`)
- Modify: `Application/Frigorino.Test/Domain/ListAggregateItemTests.cs` (`AddSeed` helper + reorder/append assertions)

- [ ] **Step 1: Update the test helper + rewrite the order tests** (in `ListAggregateItemTests.cs`)

Change the `AddSeed` helper signature from `int sortOrder` to `string rank` (default minted), and rewrite reorder/append assertions to compare ordinal string order. Replace the existing `ReorderItem_*` and append/`SortOrder` assertions with rank-based ones. Representative replacements (mirror for the rest):

```csharp
        // Helper — replaces the old sortOrder-based AddSeed.
        private static ListItem AddSeed(List list, string text, bool status = false, string? rank = null)
        {
            var item = new ListItem
            {
                Id = list.ListItems.Count + 1,
                ListId = list.Id,
                Text = text,
                Status = status,
                Rank = rank ?? FractionalIndex.GenerateKeyBetween(
                    list.ListItems.Where(i => i.Status == status).Select(i => i.Rank).LastOrDefault(), null),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            list.ListItems.Add(item);
            return item;
        }

        [Fact]
        public void ReorderItem_MidpointBetweenTwoItems_ProducesKeyStrictlyBetween()
        {
            var list = NewList();
            var item1 = AddSeed(list, "Milk", rank: "a0");
            var item2 = AddSeed(list, "Eggs", rank: "a1");
            var item3 = AddSeed(list, "Bread", rank: "a2");

            var result = list.ReorderItem(item3.Id, afterItemId: item1.Id);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item1.Rank, item3.Rank) < 0);
            Assert.True(string.CompareOrdinal(item3.Rank, item2.Rank) < 0);
        }

        [Fact]
        public void ReorderItem_MoveToTop_FromAfterIdZero_RanksBeforeFirst()
        {
            var list = NewList();
            var item1 = AddSeed(list, "Milk", rank: "a1");
            AddSeed(list, "Eggs", rank: "a2");
            var item3 = AddSeed(list, "Bread", rank: "a3");

            var result = list.ReorderItem(item3.Id, afterItemId: 0);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item3.Rank, item1.Rank) < 0);
        }

        [Fact]
        public void ReorderItem_AfterLastInSection_RanksAfterLast()
        {
            var list = NewList();
            var item1 = AddSeed(list, "Milk", rank: "a0");
            AddSeed(list, "Eggs", rank: "a1");
            var item3 = AddSeed(list, "Bread", rank: "a2");

            var result = list.ReorderItem(item1.Id, afterItemId: item3.Id);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item3.Rank, item1.Rank) < 0 == false); // item1 now after item3
            Assert.True(string.CompareOrdinal(item3.Rank, item1.Rank) < 0);
        }

        [Fact]
        public void AddItem_AppendsRankAfterLastUnchecked()
        {
            var list = NewList();
            var first = list.AddItem("Milk").Value;
            var second = list.AddItem("Eggs").Value;

            Assert.True(string.CompareOrdinal(first.Rank, second.Rank) < 0);
        }

        [Fact]
        public void ThirteenReordersIntoSameSlot_NeverCollide()
        {
            var list = NewList();
            var top = AddSeed(list, "Top", rank: "a0");
            var bottom = AddSeed(list, "Bottom", rank: "a1");
            var mover = AddSeed(list, "Mover", rank: "a2");

            // Repeatedly drop `mover` just below `top`; each reorder must yield a fresh distinct rank.
            var ranks = new HashSet<string>();
            for (var i = 0; i < 20; i++)
            {
                var r = list.ReorderItem(mover.Id, afterItemId: top.Id);
                Assert.True(r.IsSuccess);
                Assert.True(string.CompareOrdinal(top.Rank, mover.Rank) < 0);
                Assert.True(ranks.Add(mover.Rank));
            }
        }
```

Also update any other test in the file that references `SortOrder` or `SortOrderCalculator` to use `Rank` / `FractionalIndex`, and remove tests asserting exact integer midpoints (e.g. `Assert.Equal(101_000, ...)`). Update the `using` to include `Frigorino.Domain.Entities` (already present).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateItemTests"`
Expected: FAIL (compile — `Rank`/`ComputeAppendRank` semantics not yet wired; `SortOrderCalculator` references removed).

- [ ] **Step 3: Rewrite `List.cs` ordering**

Replace the private `ComputeAppendSortOrder` (lines ~578-596) with:

```csharp
        // Returns the rank for a freshly placed item in `targetStatus`'s section:
        //   - unchecked: append after the last unchecked  (key between last and null)
        //   - checked  : prepend above the first checked    (key between null and first)
        //   - empty section: GenerateKeyBetween(null, null)
        private string ComputeAppendRank(bool targetStatus)
        {
            var section = ListItems
                .Where(i => i.IsActive && i.Status == targetStatus)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();

            if (section.Count == 0)
            {
                return FractionalIndex.GenerateKeyBetween(null, null);
            }

            return targetStatus
                ? FractionalIndex.GenerateKeyBetween(null, section[0].Rank)
                : FractionalIndex.GenerateKeyBetween(section[^1].Rank, null);
        }
```

In `AddItem`, `AddMediaItem`, `UpdateItem` (status-change branch), and `ToggleItemStatus`, replace every `SortOrder = ComputeAppendSortOrder(targetStatus: X)` / `item.SortOrder = ComputeAppendSortOrder(targetStatus: X)` with `Rank = ComputeAppendRank(targetStatus: X)` / `item.Rank = ComputeAppendRank(targetStatus: X)`.

Replace `ReorderItem` (lines ~446-497) body's ordering computation with key minting:

```csharp
            var section = ListItems
                .Where(i => i.IsActive && i.Status == item.Status && i.Id != item.Id)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();

            var afterItem = afterItemId == 0
                ? null
                : section.FirstOrDefault(i => i.Id == afterItemId);
            var beforeItem = afterItem is not null
                ? section.FirstOrDefault(i => string.CompareOrdinal(i.Rank, afterItem.Rank) > 0)
                : null;

            string newRank;
            if (afterItem is null)
            {
                newRank = section.Count == 0
                    ? FractionalIndex.GenerateKeyBetween(null, null)
                    : FractionalIndex.GenerateKeyBetween(null, section[0].Rank);
            }
            else if (beforeItem is null)
            {
                newRank = FractionalIndex.GenerateKeyBetween(afterItem.Rank, null);
            }
            else
            {
                newRank = FractionalIndex.GenerateKeyBetween(afterItem.Rank, beforeItem.Rank);
            }

            item.Rank = newRank;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
```

Delete the entire `public Result CompactItems()` method (lines ~499-532). Add `using System;` if not already implied (it is, via existing `DateTime` usage).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateItemTests"`
Expected: PASS. (The aggregate compiles against `FractionalIndex` only; `SortOrderCalculator` is removed in Task 5 — `List.cs` must no longer reference it after this task.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/List.cs Application/Frigorino.Test/Domain/ListAggregateItemTests.cs
git commit -m "feat: mint List item order via FractionalIndex; drop CompactItems"
```

---

## Task 4: Rewrite `Inventory` aggregate ordering to keys — TDD

Same shape as Task 3, single section (no status).

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/Inventory.cs`
- Modify: `Application/Frigorino.Test/Domain/InventoryAggregateItemTests.cs`

- [ ] **Step 1: Update `AddSeed` + rewrite order tests** (in `InventoryAggregateItemTests.cs`)

```csharp
        private static InventoryItem AddSeed(Inventory inventory, string text, string? rank = null)
        {
            var item = new InventoryItem
            {
                Id = inventory.InventoryItems.Count + 1,
                InventoryId = inventory.Id,
                Text = text,
                Rank = rank ?? FractionalIndex.GenerateKeyBetween(
                    inventory.InventoryItems.Select(i => i.Rank).LastOrDefault(), null),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            inventory.InventoryItems.Add(item);
            return item;
        }

        [Fact]
        public void ReorderItem_MidpointBetweenTwoItems_ProducesKeyStrictlyBetween()
        {
            var inventory = NewInventory();
            var item1 = AddSeed(inventory, "Flour", rank: "a0");
            var item2 = AddSeed(inventory, "Sugar", rank: "a1");
            var item3 = AddSeed(inventory, "Salt", rank: "a2");

            var result = inventory.ReorderItem(item3.Id, afterItemId: item1.Id);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item1.Rank, item3.Rank) < 0);
            Assert.True(string.CompareOrdinal(item3.Rank, item2.Rank) < 0);
        }

        [Fact]
        public void AddItem_AppendsRankAfterLast()
        {
            var inventory = NewInventory();
            var first = inventory.AddItem("Flour", null, null).Value;
            var second = inventory.AddItem("Sugar", null, null).Value;
            Assert.True(string.CompareOrdinal(first.Rank, second.Rank) < 0);
        }
```

Rewrite the remaining `ReorderItem_*` tests (`MoveToTop`, `AfterIsLast`, `UnknownAfterId_FallsBackToTopOfSection`, `SelfAnchor_NoOp`) to assert ordinal relationships instead of integer values, and remove `SortOrderCalculator` references. Keep the `SelfAnchor_NoOp` test (it asserts the rank is unchanged: capture `item.Rank` before, assert equal after).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~InventoryAggregateItemTests"`
Expected: FAIL (compile).

- [ ] **Step 3: Rewrite `Inventory.cs` ordering**

Replace `ComputeAppendSortOrder()` (lines ~348-361) with:

```csharp
        private string ComputeAppendRank()
        {
            var section = InventoryItems
                .Where(i => i.IsActive)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();

            return section.Count == 0
                ? FractionalIndex.GenerateKeyBetween(null, null)
                : FractionalIndex.GenerateKeyBetween(section[^1].Rank, null);
        }
```

In `AddItem`, replace `SortOrder = ComputeAppendSortOrder()` with `Rank = ComputeAppendRank()`. Replace the `ReorderItem` ordering computation (lines ~262-295) with the same key-minting pattern as Task 3 Step 3 (single section — no status filter):

```csharp
            var section = InventoryItems
                .Where(i => i.IsActive && i.Id != item.Id)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();

            var afterItem = afterItemId == 0
                ? null
                : section.FirstOrDefault(i => i.Id == afterItemId);
            var beforeItem = afterItem is not null
                ? section.FirstOrDefault(i => string.CompareOrdinal(i.Rank, afterItem.Rank) > 0)
                : null;

            string newRank;
            if (afterItem is null)
            {
                newRank = section.Count == 0
                    ? FractionalIndex.GenerateKeyBetween(null, null)
                    : FractionalIndex.GenerateKeyBetween(null, section[0].Rank);
            }
            else if (beforeItem is null)
            {
                newRank = FractionalIndex.GenerateKeyBetween(afterItem.Rank, null);
            }
            else
            {
                newRank = FractionalIndex.GenerateKeyBetween(afterItem.Rank, beforeItem.Rank);
            }

            item.Rank = newRank;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
```

Delete `public Result CompactItems()` (lines ~300-324).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~InventoryAggregateItemTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/Inventory.cs Application/Frigorino.Test/Domain/InventoryAggregateItemTests.cs
git commit -m "feat: mint Inventory item order via FractionalIndex; drop CompactItems"
```

---

## Task 5: Delete `SortOrderCalculator` + compact slices + wiring

**Files:**
- Delete: `Application/Frigorino.Domain/Entities/SortOrderCalculator.cs`
- Delete: `Application/Frigorino.Features/Lists/Items/CompactItems.cs`, `Application/Frigorino.Features/Inventories/Items/CompactInventoryItems.cs`
- Modify: `Application/Frigorino.Web/Program.cs` (remove `listItems.MapCompactItems();` line 357 and `inventoryItems.MapCompactInventoryItems();` line 378)
- Delete: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useCompactListItems.ts`, `Application/Frigorino.Web/ClientApp/src/features/inventories/items/useCompactInventoryItems.ts`

- [ ] **Step 1: Delete the files and wiring**

```bash
git rm Application/Frigorino.Domain/Entities/SortOrderCalculator.cs \
       Application/Frigorino.Features/Lists/Items/CompactItems.cs \
       Application/Frigorino.Features/Inventories/Items/CompactInventoryItems.cs \
       Application/Frigorino.Web/ClientApp/src/features/lists/items/useCompactListItems.ts \
       Application/Frigorino.Web/ClientApp/src/features/inventories/items/useCompactInventoryItems.ts
```

In `Program.cs`, delete the two lines `listItems.MapCompactItems();` and `inventoryItems.MapCompactInventoryItems();`.

- [ ] **Step 2: Verify nothing references the deleted symbols**

Run: `grep -rn "SortOrderCalculator\|CompactItems\|CompactInventoryItems\|MapCompactItems\|MapCompactInventoryItems" Application --include=*.cs`
Expected: no matches outside generated `src/lib/api` (which is regenerated in Task 9) and `knowledge/`/`docs` notes. If a `.cs` match remains, remove it.

- [ ] **Step 3: Build the backend**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor: remove SortOrderCalculator + orphaned compact endpoints/hooks"
```

---

## Task 6: Collision-retry helper + wrap minting handlers

Mirrors the existing `ExpiryNotificationScan.cs` unique-violation catch. The aggregate re-mints from current in-memory neighbours, so a retry must **reload the aggregate fresh** (to see the committed neighbour) and re-apply.

**Files:**
- Create: `Application/Frigorino.Features/Items/RankRetry.cs`
- Modify: `Application/Frigorino.Features/Lists/Items/{CreateItem,CreateMediaItem,UpdateItem,ToggleItemStatus,ReorderItem,RestoreItem}.cs`
- Modify: `Application/Frigorino.Features/Inventories/Items/{CreateInventoryItem,ReorderInventoryItem,RestoreInventoryItem}.cs`

- [ ] **Step 1: Create `RankRetry.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Frigorino.Features.Items
{
    // Concurrent reorders/appends into the same slot can mint the same Rank; the partial unique
    // index (ListId/InventoryId, [Status,] Rank WHERE IsActive) rejects the duplicate with SQLSTATE
    // 23505. The aggregate re-mints from current neighbours, so on conflict we reload fresh state
    // and re-apply. Bounded — a true unresolved race surfaces as a thrown exception (500/conflict).
    public static class RankRetry
    {
        public const int MaxAttempts = 3;

        // `apply` must: (re)load the aggregate into `db`, invoke the aggregate method, and return
        // its FluentResults result-bearing value (or null on a domain failure the caller maps).
        // It is re-invoked from scratch on each attempt.
        public static async Task<T> SaveWithRetryAsync<T>(
            ApplicationDbContextMarker _,
            Func<Task<T>> applyAndSave,
            CancellationToken ct)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await applyAndSave();
                }
                catch (DbUpdateException ex)
                    when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
                          && attempt < MaxAttempts)
                {
                    // Fresh reload happens inside applyAndSave on the next iteration.
                }
            }
        }
    }

    // Marker so callers don't need to thread the concrete DbContext type into this Features helper.
    public sealed class ApplicationDbContextMarker
    {
        public static readonly ApplicationDbContextMarker Instance = new();
    }
}
```

> Simplify if preferred: the marker is only to keep `RankRetry` free of an Infrastructure type reference. If `Frigorino.Features` already references `Frigorino.Infrastructure` (it does — handlers use `ApplicationDbContext`), drop the marker and pass nothing. Use this simpler signature instead:

```csharp
        public static async Task<T> SaveWithRetryAsync<T>(Func<Task<T>> applyAndSave, CancellationToken ct)
        {
            for (var attempt = 1; ; attempt++)
            {
                try { return await applyAndSave(); }
                catch (DbUpdateException ex)
                    when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
                          && attempt < MaxAttempts)
                { }
            }
        }
```

Use the simpler form. Confirm `Frigorino.Features.csproj` references `Npgsql` (transitively via Infrastructure → EF Npgsql; if the `Npgsql` namespace doesn't resolve, add `<PackageReference Include="Npgsql" Version="..." />` matching the Infrastructure pin, or reference `Npgsql.EntityFrameworkCore.PostgreSQL`).

- [ ] **Step 2: Wrap `ReorderItem.cs` (list) — pattern for all reorder/mint handlers**

Replace the load→mutate→save section (lines ~40-61) so the reload+apply+save is the retried unit:

```csharp
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                var list = await db.Lists
                    .Include(l => l.ListItems)
                    .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
                if (list is null)
                {
                    return (ListItemResponse?)null; // not found -> mapped below
                }

                var result = list.ReorderItem(itemId, request.AfterId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError)
                    {
                        return null;
                    }
                    throw new InvalidOperationException(
                        $"ReorderItem cannot map error of type {result.Errors[0].GetType().Name}.");
                }

                await db.SaveChangesAsync(ct);
                return ListItemResponse.From(result.Value);
            }, ct);

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
```

> Because a retry reloads via the same `db` scope, call `db.ChangeTracker.Clear()` at the top of the lambda's second+ iterations is unnecessary here — `FirstOrDefaultAsync` with `Include` returns the tracked graph and `ReorderItem` mutates it; on a unique violation the failed `SaveChangesAsync` leaves the entity `Modified`, and the reload returns the same tracked instance with the **DB's** committed neighbour values only after a fresh query against the store. To guarantee fresh neighbour data on retry, add `db.ChangeTracker.Clear();` as the **first line inside the lambda**. Include it.

So the lambda's first line is:
```csharp
                db.ChangeTracker.Clear();
```

- [ ] **Step 3: Apply the same wrapping to the other minting handlers**

For each handler below, move its existing `load aggregate → call aggregate method → SaveChangesAsync → project response` into a `RankRetry.SaveWithRetryAsync(async () => { db.ChangeTracker.Clear(); ... }, ct)` lambda, preserving each handler's exact response type, status codes, and any extra logic (e.g. `ToggleItemStatus`'s `Promote` suggestion, `CreateItem`'s extraction enqueue, `CreateMediaItem`'s blob handling). Keep the membership/existence pre-checks that don't mint a rank OUTSIDE the lambda where they already are, but the aggregate reload must be INSIDE.

Handlers to wrap:
- `Lists/Items/CreateItem.cs` (`list.AddItem`)
- `Lists/Items/CreateMediaItem.cs` (`list.AddMediaItem`)
- `Lists/Items/UpdateItem.cs` (`list.UpdateItem` — only re-mints when status flips, but the unique index can still reject; wrap it)
- `Lists/Items/ToggleItemStatus.cs` (`list.ToggleItemStatus`)
- `Inventories/Items/CreateInventoryItem.cs` (`inventory.AddItem`)
- `Inventories/Items/ReorderInventoryItem.cs` (`inventory.ReorderItem`)

> For handlers with side effects that must NOT repeat on retry (e.g. blob upload in `CreateMediaItem`, extraction enqueue in `CreateItem`): keep those side effects OUTSIDE/AFTER the retried save. Only the DB mint+save is retried. In `CreateMediaItem`, the blob is already stored before `AddMediaItem`; that's fine (retry only re-runs the EF insert). In `CreateItem`, perform the extraction enqueue AFTER the successful save returns.

- [ ] **Step 4: Wrap `RestoreItem.cs` (list) + `RestoreInventoryItem.cs` — keep undo position, re-mint only on collision**

Restore reactivates a soft-deleted row carrying its original `Rank`. Normally that rank is still free (preserves the item's old position for undo). If an active item took that rank while it was deleted, the unique index rejects it; on that conflict, re-mint to the bottom of the section. Implement by wrapping in `RankRetry` and, when the retry reloads, re-minting:

In `RestoreItem.cs`, after `list.RestoreItem(itemId)` succeeds, the item keeps its stored `Rank`. Wrap the save in `RankRetry.SaveWithRetryAsync`. Add a second aggregate method to `List` to re-place a restored item on conflict — OR simpler: on the retried iteration, set the restored item's `Rank` to a fresh append. Add this method to `List.cs`:

```csharp
        // Re-mints a restored item's rank when its original collides with a live item.
        public Result<ListItem> ReplaceRestoredItemRank(int itemId)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(new EntityNotFoundError($"List item {itemId} not found."));
            }
            item.Rank = ComputeAppendRank(item.Status);
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }
```

In the handler lambda, track attempt count: first attempt restores with original rank; on a retried attempt call `ReplaceRestoredItemRank`. The simplest robust form:

```csharp
            var attemptedReplace = false;
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();
                var list = await db.Lists.Include(l => l.ListItems)
                    .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
                if (list is null) return (ListItemResponse?)null;

                var result = list.RestoreItem(itemId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException($"RestoreItem cannot map {result.Errors[0].GetType().Name}.");
                }
                if (attemptedReplace)
                {
                    list.ReplaceRestoredItemRank(itemId);
                }
                attemptedReplace = true;
                await db.SaveChangesAsync(ct);
                return ListItemResponse.From(result.Value);
            }, ct);
            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
```

Add the analogous `ReplaceRestoredItemRank(int itemId)` (no status arg) to `Inventory.cs` and wire `RestoreInventoryItem.cs` the same way.

- [ ] **Step 5: Build + run the full domain test suite**

Run: `dotnet build Application/Frigorino.sln && dotnet test Application/Frigorino.Test`
Expected: Build succeeded; all unit tests PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features Application/Frigorino.Domain/Entities/List.cs Application/Frigorino.Domain/Entities/Inventory.cs
git commit -m "feat: retry rank minting on unique-violation; preserve undo position on restore"
```

---

## Task 7: Response DTOs + read-path ordering → `Rank`

**Files:**
- Modify: `Application/Frigorino.Features/Lists/Items/ListItemResponse.cs`, `Application/Frigorino.Features/Inventories/Items/InventoryItemResponse.cs`
- Modify: `Application/Frigorino.Features/Lists/Items/GetItems.cs`, `Application/Frigorino.Features/Inventories/Items/GetInventoryItems.cs`

- [ ] **Step 1: Swap `SortOrder` → `Rank` in `ListItemResponse`**

Change the positional record param `int SortOrder` → `string Rank`, and in both `From` and `ToProjection` replace `item.SortOrder`/`i.SortOrder` with `item.Rank`/`i.Rank`. Keep the param position (it's positional — update every call site, but `From`/`ToProjection` are the only constructors and are in this file).

- [ ] **Step 2: Swap `SortOrder` → `Rank` in `InventoryItemResponse`** (same edit, `int SortOrder` → `string Rank`).

- [ ] **Step 3: Order reads by `Rank`, `Id`**

In `GetItems.cs` replace lines 45-46:
```csharp
                .OrderBy(i => i.Status)
                .ThenBy(i => i.Rank)
                .ThenBy(i => i.Id)
```

In `GetInventoryItems.cs` replace lines 45-46:
```csharp
                .OrderBy(i => i.Rank)
                .ThenBy(i => i.Id)
```
(Remove the old `.ThenByDescending(i => i.CreatedAt)`.)

- [ ] **Step 4: Build**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded. (`item.SortOrder` is still a valid property — dead — so no compile break. Confirm no remaining `SortOrder` reads in `Frigorino.Features` via `grep -rn "SortOrder" Application/Frigorino.Features`.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Features
git commit -m "feat: expose Rank on item responses; order reads by Rank, Id"
```

---

## Task 8: One-time startup backfill

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/RankBackfill.cs`
- Modify: `Application/Frigorino.Web/Program.cs` (call after `MigrateAsync`)

- [ ] **Step 1: Create `RankBackfill.cs`**

Reads existing rows ordered by the dead `SortOrder`, mints a contiguous key sequence per section, and writes `Rank` via `ExecuteUpdateAsync` so the `ApplicationDbContext.SaveChangesAsync` timestamp-stamping is bypassed (`UpdatedAt` is NOT touched). Idempotent: guarded on any `Rank == null`.

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // One-time expand-phase backfill: assign fractional-index Rank to rows created before the
    // Rank column existed, derived from the legacy SortOrder ordering. Does NOT bump UpdatedAt
    // (ExecuteUpdateAsync bypasses SaveChangesAsync stamping) — deliberate, mirrors the retention
    // concern that retired the old RecalculateSortOrderTask sweep. Runs at startup before serving;
    // a no-op once every row has a Rank. Removed in the deferred cleanup once stage+prod are filled.
    public static class RankBackfill
    {
        public static async Task RunAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct = default)
        {
            await BackfillListItemsAsync(db, logger, ct);
            await BackfillInventoryItemsAsync(db, logger, ct);
        }

        private static async Task BackfillListItemsAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct)
        {
            if (!await db.ListItems.AnyAsync(i => i.Rank == null, ct))
            {
                return;
            }

            // Group by (list, status) section; order within section by the legacy SortOrder.
            var rows = await db.ListItems
                .OrderBy(i => i.ListId).ThenBy(i => i.Status).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new { i.Id, i.ListId, i.Status, i.Rank })
                .ToListAsync(ct);

            var total = 0;
            foreach (var section in rows.GroupBy(r => new { r.ListId, r.Status }))
            {
                var ordered = section.ToList();
                var keys = FractionalIndex.GenerateKeysBetween(null, null, ordered.Count);
                for (var k = 0; k < ordered.Count; k++)
                {
                    var id = ordered[k].Id;
                    var rank = keys[k];
                    await db.ListItems.Where(i => i.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(i => i.Rank, rank), ct);
                    total++;
                }
            }
            logger.LogInformation("RankBackfill: assigned Rank to {Count} list items.", total);
        }

        private static async Task BackfillInventoryItemsAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct)
        {
            if (!await db.InventoryItems.AnyAsync(i => i.Rank == null, ct))
            {
                return;
            }

            var rows = await db.InventoryItems
                .OrderBy(i => i.InventoryId).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new { i.Id, i.InventoryId, i.Rank })
                .ToListAsync(ct);

            var total = 0;
            foreach (var section in rows.GroupBy(r => r.InventoryId))
            {
                var ordered = section.ToList();
                var keys = FractionalIndex.GenerateKeysBetween(null, null, ordered.Count);
                for (var k = 0; k < ordered.Count; k++)
                {
                    var id = ordered[k].Id;
                    var rank = keys[k];
                    await db.InventoryItems.Where(i => i.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(i => i.Rank, rank), ct);
                    total++;
                }
            }
            logger.LogInformation("RankBackfill: assigned Rank to {Count} inventory items.", total);
        }
    }
}
```

> Note: `i.Rank == null` compiles because the column is nullable in the DB even though the CLR property is non-nullable `string`. EF translates the comparison to SQL `IS NULL`. If the C# nullable analyzer warns, compare via `EF.Property<string?>(i, "Rank") == null` instead. Prefer the `EF.Property` form to silence the warning cleanly.

Replace both `i.Rank == null` guards and the projection's reliance on rank with the `EF.Property<string?>(i, nameof(ListItem.Rank)) == null` form if needed.

- [ ] **Step 2: Call the backfill in `Program.cs` after migrate**

In the migration block (around lines 208-215), after `await context.Database.MigrateAsync();` and still inside the scope, add:

```csharp
        var backfillLogger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RankBackfill");
        await Frigorino.Infrastructure.Services.RankBackfill.RunAsync(context, backfillLogger);
```

- [ ] **Step 3: Build**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/RankBackfill.cs Application/Frigorino.Web/Program.cs
git commit -m "feat: one-time startup backfill of Rank from legacy SortOrder"
```

---

## Task 9: Regenerate API client + frontend rework

**Files:**
- Run `npm run api` (regenerates `src/lib/api/*`)
- Delete: `src/common/sortOrder.ts`
- Modify: `src/features/lists/items/useReorderListItem.ts`, `useToggleListItemStatus.ts`, `useCreateListItem.ts`
- Modify: `src/features/inventories/items/useReorderInventoryItem.ts`, `useCreateInventoryItem.ts`
- Modify: `src/components/sortables/SortableList.tsx`

- [ ] **Step 1: Regenerate the typed client** (from `Application/Frigorino.Web/ClientApp/`)

Run: `npm run api`
Expected: `src/lib/api/types.gen.ts` now has `rank: string` (not `sortOrder: number`) on `ListItemResponse`/`InventoryItemResponse`, and the compact endpoints are gone from `sdk.gen.ts`/`react-query.gen.ts`.

- [ ] **Step 2: Delete the client sort math**

```bash
git rm src/common/sortOrder.ts
```

- [ ] **Step 3: `SortableList.tsx` — custom mode preserves server array order**

Remove the `sortOrder` field from `SortableItemInterface` (line ~48). In the `sortItems` function (lines ~129-160), replace the `custom`/default branch so it returns items unchanged (server already ordered them by `Rank`); keep the `expiryDateAsc`/`expiryDateDesc` branches:

```tsx
        const sortItems = (itemsToSort: T[]) => {
            if (skipInternalSort) {
                return itemsToSort;
            }
            if (sortMode === "expiryDateAsc") {
                return [...itemsToSort].sort((a, b) => { /* keep existing expiry logic */ });
            }
            if (sortMode === "expiryDateDesc") {
                return [...itemsToSort].sort((a, b) => { /* keep existing expiry logic */ });
            }
            // "custom" / default: trust server (Rank) order as delivered in the array.
            return itemsToSort;
        };
```

- [ ] **Step 4: `useReorderListItem.ts` — splice the array, no key math**

Replace the `onMutate` optimistic body so it moves the dragged item to its new array position (after `afterId`, or to the top of its status section when `afterId` is 0/falsy) instead of computing `sortOrder`:

```tsx
        onMutate: async (variables) => {
            const queryKey = getItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    listId: variables.path.listId,
                },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousItems =
                queryClient.getQueryData<ListItemResponse[]>(queryKey);

            queryClient.setQueryData<ListItemResponse[]>(queryKey, (old) => {
                if (!old) return old;
                const moved = old.find((i) => i.id === variables.path.itemId);
                if (!moved) return old;

                const others = old.filter((i) => i.id !== moved.id);
                const afterId = variables.body.afterId;
                if (!afterId) {
                    // Top of the moved item's status section.
                    const firstSameStatus = others.findIndex(
                        (i) => i.status === moved.status,
                    );
                    const insertAt =
                        firstSameStatus === -1 ? others.length : firstSameStatus;
                    others.splice(insertAt, 0, moved);
                    return others;
                }
                const anchorIdx = others.findIndex((i) => i.id === afterId);
                others.splice(
                    anchorIdx === -1 ? others.length : anchorIdx + 1,
                    0,
                    moved,
                );
                return others;
            });
            return { previousItems };
        },
```

Remove the now-unused `computeAppendSortOrder`/`computeReorderSortOrder` imports. Keep `onError`/`onSettled` unchanged.

- [ ] **Step 5: `useReorderInventoryItem.ts`** — apply the same array-splice rewrite (single section, so the `afterId` falsy branch inserts at index 0): top branch is simply `others.unshift(moved)`.

- [ ] **Step 6: `useToggleListItemStatus.ts`** — drop `computeAppendSortOrder`. The optimistic update flips `status` and re-positions the item in the array to mirror the server: checked → top of checked section; unchecked → bottom of unchecked section. Replace the `sortOrder` computation/mutation with an array move:

```tsx
            queryClient.setQueryData<ListItemResponse[]>(queryKey, (old) => {
                if (!old) return old;
                const moved = old.find((i) => i.id === variables.path.itemId);
                if (!moved) return old;
                const newStatus = !moved.status;
                const updated = { ...moved, status: newStatus };
                const others = old.filter((i) => i.id !== moved.id);
                if (newStatus) {
                    // Checked: prepend above the first checked item.
                    const firstChecked = others.findIndex((i) => i.status);
                    others.splice(
                        firstChecked === -1 ? others.length : firstChecked,
                        0,
                        updated,
                    );
                } else {
                    // Unchecked: append after the last unchecked item.
                    let lastUnchecked = -1;
                    others.forEach((i, idx) => {
                        if (!i.status) lastUnchecked = idx;
                    });
                    others.splice(lastUnchecked + 1, 0, updated);
                }
                return others;
            });
```

(Preserve whatever the hook returns for `onError`/`onSettled` and the promotion handling, if any.)

- [ ] **Step 7: `useCreateListItem.ts` / `useCreateInventoryItem.ts`** — remove the `sortOrder: last + 1` field from the optimistic item object (the new item is appended to the cache array; server returns the real `rank` on refetch). Remove the `lastUncheckedSortOrder`/`lastSortOrder` computation. Ensure the optimistic item is pushed to the end of the array (lists: end is fine — it's unchecked and renders after existing unchecked; inventory: end of array).

- [ ] **Step 8: Type-check, lint, format**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: no errors. Fix any remaining `sortOrder` references the compiler flags. Then `npm run prettier` (write).

Run: `grep -rn "sortOrder\|SortOrder\|computeAppendSortOrder\|computeReorderSortOrder" src --exclude-dir=lib`
Expected: no matches outside generated `src/lib`.

- [ ] **Step 9: Build the SPA** (so the integration harness, which serves `ClientApp/build`, picks up changes)

Run: `npm run build`
Expected: build succeeds, outputs to `ClientApp/build`.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat: client reorders splice cache array; drop sortOrder math; regen API client"
```

---

## Task 10: Integration tests (reorder persistence + collision)

**Files:**
- Modify/inspect: `Application/Frigorino.IntegrationTests/Slices/Lists/ListItemApiSteps.cs` + its `.feature`
- Modify/inspect: `Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItemApiSteps.cs` + its `.feature`

The existing reorder steps (`When I PATCH "X" after "Y" ...`, `Then the API items of "L" appear in order: "..."`) are rank-agnostic — they assert on item **text** order via the API, so they already validate the new scheme. No step code changes needed unless a step asserts on `sortOrder` (grep to confirm).

- [ ] **Step 1: Confirm no IT step asserts on `sortOrder`**

Run: `grep -rn "sortOrder\|SortOrder" Application/Frigorino.IntegrationTests`
Expected: no matches. If any, switch them to text-order assertions.

- [ ] **Step 2: Add a "reorder sticks across refetch" scenario** (the regression this fixes)

In the lists `.feature` that drives reorder, add a scenario that reorders an item into a tight spot several times and re-reads, asserting the final order holds. Use the existing step vocabulary, e.g.:

```gherkin
  Scenario: Repeated reorders into the same slot persist
    Given a list "Groceries" with items "A,B,C,D"
    When I PATCH "D" after "A" in "Groceries" via the API
    And I PATCH "C" after "A" in "Groceries" via the API
    And I PATCH "B" after "A" in "Groceries" via the API
    Then the API items of "Groceries" appear in order: "A,B,C,D"
```

(Adjust the expected final order to match the semantics: each "after A" insert places the moved item immediately below A, so the last one moved ends up directly under A.) Verify the expected string by reasoning through the inserts; if unsure, run once and read the actual order, then encode it.

- [ ] **Step 3: Run the integration tests**

Run: `dotnet test Application/Frigorino.IntegrationTests`
Expected: PASS. (Requires Docker for Testcontainers — if the daemon is down, ask the user to start Docker Desktop.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.IntegrationTests
git commit -m "test: reorder persistence across refetch (fractional index)"
```

---

## Task 11: Full verification

- [ ] **Step 1: Full solution tests**

Run: `dotnet test Application/Frigorino.sln`
Expected: all PASS (unit + integration). Capture pass/fail counts — do not trust a piped exit code.

- [ ] **Step 2: Frontend gates** (from `ClientApp/`)

Run: `npm run lint && npm run tsc && npm run prettier:check && npm run build`
Expected: all clean.

- [ ] **Step 3: Docker build** (catches Dockerfile/SPA/pipeline drift)

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: image builds. (If the daemon is down, ask the user to start Docker Desktop.)

- [ ] **Step 4: Manual browser verify** (static checks won't catch a render-order bug)

Bring up the dev stack (`/dev-up`), then with Playwright MCP or by hand:
1. Open a list, drag an item into a tight spot ~15 times in a row, refetch (reload) — order must hold every time (the old scheme reshuffled after ~13).
2. Check/uncheck items — they jump to the correct section end/top and stick after reload.
3. Create + delete + undo an item — undo restores it near its original position.
4. Repeat the drag test in an inventory.

- [ ] **Step 5: Remove the TECH_DEBT entry**

Delete the "Sparse sort-order scheme has no rebalancer left" item from `TECH_DEBT.md` (the expand half ships the fix; note the deferred `sort_order` column drop as a one-line follow-up in TECH_DEBT or IDEAS if you want it tracked).

- [ ] **Step 6: Final commit**

```bash
git add TECH_DEBT.md
git commit -m "docs: clear sort-order tech-debt item (fractional index shipped)"
```

---

## Deferred cleanup (NOT part of this plan's acceptance)

A separate later change, once stage + prod have run the backfill and `rank` is confirmed fully populated:
- Remove the `SortOrder` property from `ListItem`/`InventoryItem` and their EF configs (and the now-unused `SortOrder` indexes).
- Generate migration `DropItemSortOrder` (`DropColumn("SortOrder")` on both tables).
- Delete `RankBackfill.cs` and its `Program.cs` call.
- Optionally tighten `Rank` to `NOT NULL` at the DB level.

---

## Self-Review notes

- **Spec coverage:** algorithm (T1), entity/EF/index/collation (T2), aggregate minting + compact deletion (T3/T4/T5), read-path + Id tiebreaker (T7), collision retry (T6), server-authoritative + client simplification (T9), backfill no-UpdatedAt-bump (T8), expand-only migration + deferred contract (T2/T8/deferred). All spec sections map to a task.
- **Restore collision** (not in spec, found in exploration) handled in T6 Step 4 to avoid breaking undo.
- **`SortableList` client-sort** (not in spec, found in exploration) resolved in T9 Step 3 (custom mode trusts server order) — matches the spec's anticipated fallback.
- **Type consistency:** `Rank` (string) used uniformly; `ComputeAppendRank`, `ReplaceRestoredItemRank`, `RankRetry.SaveWithRetryAsync`, `RankBackfill.RunAsync`, `GenerateKeyBetween`/`GenerateKeysBetween` names consistent across tasks.
