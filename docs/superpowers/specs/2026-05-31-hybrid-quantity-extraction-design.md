# Hybrid deterministic-then-LLM quantity extraction

**Date:** 2026-05-31
**Status:** Design approved (pending spec review)
**Tech debt item:** "Hybrid deterministic-then-LLM quantity extraction" (TECH_DEBT.md)

## Problem

v1 of inline quantity extraction (`feat/quantity-inline-extraction`) is LLM-only: every
digit-bearing list-item add pays an OpenAI round-trip plus the ~4s client poll window, even
for trivial inputs like `2 milk`. Junk input wastes AI on *both* the extractor (digit path)
and the classifier (no-digit path) — e.g. a pasted URL is classified as a "product".

The cost is ongoing token spend and a slower-than-necessary perceived UX for the easy cases
the LLM is overkill for.

## Goal

Parse the unambiguous cases deterministically and for free, synchronously, before any AI is
touched; reserve the LLM for genuinely ambiguous input; and skip AI entirely for input that
is obviously not a product. Keep correctness the priority: a deterministic write is immediate
and authoritative (no LLM correction behind it), so the parser must only fire when it is
highly confident.

## Architecture

A pure-domain **router** decides what to do with each new/edited item's text; the create/update
slices execute the synchronous part of that decision in the same DB write; a shrunken trigger
handles the asynchronous fan-out (enqueue LLM / classify / nothing).

```
slice (CreateItem / UpdateItem)
  → ItemTextRouter.Analyze(rawText)   [pure, Domain]
  → list.AddItem(cleanName, quantity?) within the existing SaveChanges
  → quantityTrigger.OnItemRouted(householdId, listId, itemId, analysis)   [async fan-out, Infra]
```

### Why a pure router (rejected alternative)

Alternative considered: grow the guard + parse logic inside `QueueingQuantityExtractionTrigger`
and have it do its own synchronous DB write. Rejected — it forces a second DB round-trip, the
`201`/`200` response can't reflect the resolved values, and the pure parse logic gets buried in
Infrastructure (mock-heavy tests). The router keeps the decision logic pure (unit-testable with
no mocks, in `Frigorino.Test/Domain`) and the side-effects in Infrastructure, honouring the
existing layering enforced by ArchUnitNET.

## The guard chain — `ItemTextRouter.Analyze(rawText)`

Pure function in `Frigorino.Domain`. Evaluates guards **in order; first match wins**, returning
one of four routes. Runs unconditionally (it's free) regardless of whether AI extraction is
enabled.

1. **SkipAi** — store raw text, no extraction, **no classification**. Triggered by (conservative,
   high-precision signals only):
   - empty after trim, or
   - punctuation/emoji-only (no letter or digit after trim), or
   - contains an `http(s)://` or `www.` token (URL), or
   - exceeds the length ceiling: **> 120 characters OR > 15 words** (generous — well above any
     real product name, well below the 500-char `ListItem.Text` cap; guards obvious nonsense
     like 300-char pastes).

   Runs *before* the digit gate, so a URL/junk string that happens to contain digits is skipped
   on both the extractor and classifier paths.

2. **Resolved** — `Quantity.TryParse` succeeds (scope below). Store clean name + parsed quantity,
   classify the clean name. No LLM, no poll.

3. **NeedsExtraction** — text has a digit but no confident parse. Enqueue `ExtractQuantityJob`
   (today's LLM path: extract → rewrite to clean name + set quantity → chain classification).

4. **ClassifyOnly** — no digit, not junk. Classify the raw text (today's no-digit behavior).

### Result shape

```csharp
public enum ItemTextRoute { SkipAi, Resolved, NeedsExtraction, ClassifyOnly }

public readonly record struct ItemTextAnalysis(
    ItemTextRoute Route,
    string CleanName,     // == raw text for every route except Resolved
    Quantity? Quantity);  // non-null only for Resolved
```

## `Quantity.TryParse` — scope (Option A, ultra-conservative)

```csharp
public static bool TryParse(string text, out string cleanName, out Quantity quantity);
```

A number is treated as a quantity **only** when it is a standalone token that is either glued
to / followed by a known metric unit, or is the leading bare integer count:

| Pattern | Example | Result |
|---|---|---|
| Leading number + unit | `2kg flour`, `500 ml milk`, `1,5 l juice`, `2 l milk` | name + qty |
| Trailing number + unit | `flour 2kg`, `milk 500ml` | name + qty |
| Leading bare integer | `3 milk` (whitespace required after the number) | name + qty `Piece` |

- Units (case-insensitive): `g → Gram`, `kg → Kilogram`, `ml → Milliliter`, `l → Liter`.
  Metric symbols only — universal across the EN/DE inputs the app accepts, so no German word list.
- Decimal separator: both comma (`1,5`) and dot (`1.5`).
- After a match, `cleanName` is the remaining name trimmed; quantity is built via the existing
  `Quantity.Create` (so magnitude/scale invariants still apply — a parse that fails `Create`
  returns `false`).

### Deliberately deferred to the LLM (`TryParse` returns false)

- **Brand-digits**: `7up`, `WD-40`, `E45 cream` — a digit glued to letters that aren't a known
  unit is never a quantity. This glued-to-non-unit rule is what makes brand-digits safe.
- **Trailing bare integers**: `Coca Cola 2` (could be a model/edition number).
- **Mid-string quantities**, **container words** (`2 bottles of beer` — the "of beer" strip is
  fiddly; the LLM already does it well).
- **Number-only, no product**: `2kg` alone (empty clean name) → not resolved.

## Wiring & changes

- **`Frigorino.Domain`**
  - `Quantity.TryParse` (new, pure).
  - `ItemTextRouter.Analyze` + `ItemTextRoute` / `ItemTextAnalysis` (new, pure).
  - `List.AddItem` gains an optional `Quantity? quantity = null` parameter so the Resolved case
    writes name + quantity in one call / one `SaveChanges` (no rewrite, no second trip).
- **`Frigorino.Infrastructure`**
  - `IQuantityExtractionTrigger.OnItemEntered` → `OnItemRouted(householdId, listId, itemId, ItemTextAnalysis)`.
    The two impls map the route to side-effects:
    - **Queueing** (AI on): `SkipAi`→nothing, `Resolved`→classify(cleanName),
      `NeedsExtraction`→enqueue `ExtractQuantityJob`, `ClassifyOnly`→classify(raw).
    - **Null** (extraction off): same, except `NeedsExtraction`→classify(raw) (can't extract,
      still classify). Routes 1/2/4 behave identically to Queueing — so even with extraction
      off, URLs are skipped and easy quantities are parsed for free.
- **`Frigorino.Features`**
  - `CreateItem`: `Analyze` → `AddItem(cleanName, quantity?)` → save → `OnItemRouted`. The
    `201` response now carries the resolved clean name + quantity directly (no poll flicker).
  - `UpdateItem`: unchanged guard at line 81 — the router engages **only** on the
    text-changed / no-explicit-quantity branch. On a `SkipAi` edit the existing quantity is
    **left untouched** (the user is authoritative for syncing text ↔ quantity when editing by
    hand; matches the existing "user authoritative" comment).

## Testing

- **Domain unit tests** (`Frigorino.Test/Domain`, no mocks): `Quantity.TryParse` truth table
  (every Resolved pattern + every deferred case incl. `7up`/`WD-40`/`E45 cream`/`Coca Cola 2`/
  `2kg`-alone); `ItemTextRouter.Analyze` route selection (URL, punctuation-only, length ceiling
  boundary, digit/no-digit, resolved).
- **Infrastructure tests**: update `QuantityExtractionTriggerTests` for the new
  `OnItemRouted` contract (route → enqueue / classify / nothing, for both Queueing and Null).
  `ExtractQuantityJob` tests stay as-is (job behavior unchanged).
- **Verification gate**: `dotnet test Application/Frigorino.sln` + `docker build`.

## Out of scope

- No German/English unit *words* (only metric symbols) — deferred-to-LLM territory.
- No length-based product/note distinction beyond the nonsense ceiling.
- No change to `ExtractQuantityJob`, the channels queue, or the classification engine internals.
```
