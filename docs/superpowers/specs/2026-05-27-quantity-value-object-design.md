# Quantity as a domain value object (value + unit) — design

- **Date:** 2026-05-27
- **Status:** Approved (design); **planning deferred** — see Sequencing below
- **Branch:** TBD (off `stage` when picked up)

## Summary

`Quantity` is a free-text `string?` today on both `ListItem` (`Frigorino.Domain/Entities/ListItem.cs:13`)
and `InventoryItem` (`InventoryItem.cs:16`). A user types "2 kg" or "500ml" and it is opaque — the app
cannot sum, compare, convert, or reason about it.

This feature replaces that free text with a structured **value object**: a numeric `Value` plus a
`Unit` from a fixed set. The whole point of v1 is **clean input at entry time** — the structured
picker guides the user to a real value + unit so the data is trustworthy for later features
(conversions, duplicate-merge, low-stock thresholds), rather than retrofitting structure onto
arbitrary strings after the fact. Those payoff features are **explicit follow-ups**; v1's deliverable
is the clean data model + the picker that produces it.

## Goals

- Model quantity as a value object: numeric `Value` (decimal) + `Unit` (fixed enum).
- Guide users to enter structured quantity via a redesigned picker (no free-typed units).
- Keep the database model flat — two nullable scalar columns per item, no owned/complex EF type.
- Ship a reusable free-text→`Quantity` parser (used by the migration backfill, reusable later).
- Leave room for the future classifier to drive unit suggestions and item-specific containers.

## Non-goals / out of scope (follow-up tasks)

- **Unit conversions** (e.g. "you have 1.5 kg, recipe needs 200 g"). The unit metadata (dimension +
  base-factor) is defined in v1, but no conversion API is exposed yet.
- **Duplicate-merge** of inventory items by name+unit.
- **Low-stock thresholds** / shopping-list math.
- **Item classification** itself — but the model is designed so a later classifier can (a) suggest
  units per item type (milk → bottle/l first) and (b) give a count unit a per-item measured equivalent
  ("milk → bottle = 1 l here") to enable cross-unit math. Nothing in v1 blocks this.
- **Inline parsing of the item text field** ("2 apples", "milk 500ml" → name + quantity). See
  "Future hooks" — v1 only ships the quantity-token parser this would reuse, not the name extraction.

## Key decisions & rationale

1. **Fixed enum, richer set — not dimensions-only, not an extensible table.**
   `QuantityUnit ∈ { Gram, Kilogram, Milliliter, Liter, Piece, Pack, Can, Bottle, Bag }`.
   *Rationale: serializes as a string via the existing global `JsonStringEnumConverter` (matches the
   wire convention the generated TS client expects), maps to a flat column, no join. Covers the
   grocery/pantry vocabulary users actually use (cans/packs/bottles) without the infra of a unit
   table. Can graduate to a table later if truly user-defined custom units are ever needed.*

2. **Unit metadata lives in the domain.** Each unit → `Dimension` (`Mass` / `Volume` / `Count`) plus a
   base-conversion factor (grams for mass, millilitres for volume). Count units carry no factor.
   v1 *defines* this metadata but exposes **no conversion API** — conversions are a follow-up.

3. **`Quantity` is a pure domain value object, NOT an EF-mapped owned/complex type.** A readonly record
   struct (`decimal Value`, `QuantityUnit Unit`) used by aggregate methods for validate/format/parse.
   *Rationale: matches the existing "entities are flat property bags, logic on the parent aggregate"
   style; sidesteps EF Core nullable-complex-type limitations (quantity is optional). Aligns with the
   flat-schema preference — no owned-type table, no complex-type config.*

4. **Two flat nullable columns per item, replacing the string column.** Drop `Quantity string?` from
   both entities; add `QuantityValue decimal?` + `QuantityUnit` (nullable enum). Remove the
   `QuantityMaxLength` constants and the `HasMaxLength` EF config.
   **Invariant: both columns set, or both null** (= no quantity). Enforced in the aggregate.

5. **Strictly value + unit going forward — no free-text escape hatch.** Quantity is either a numeric
   value + a unit, or absent (it stays optional). Oddities like "to taste" / "a handful" belong in the
   item *text*, not the quantity. *Rationale: keeps the data 100% clean for future math/merging;
   consistent with dropping unparseable legacy values (decision 6). No permanent raw column, no dual
   rendering path.*

6. **Migration drops unparseable legacy values.** The backfill best-effort parses existing free-text
   into `value + unit`; anything that does not parse is set to **null**.
   *Accepted, deliberate data-loss tradeoff — including on stage/prod, which carry real client data.*
   The user chose this over preserving unparseable text in a raw column or flagging items for re-entry,
   preferring the simplest schema (no permanent raw column).

7. **Atomic nested DTO on the wire.** `QuantityDto(decimal Value, QuantityUnit Unit)`, nullable on
   requests/responses, so value and unit can never be transmitted apart — the both-or-null invariant
   is expressed in the DTO shape, not just validated.

8. **The free-text→`Quantity` parser is a reusable first-class component, not a migration one-off.**
   (`Quantity.TryParse(string)` or a small `QuantityParser`.) v1 uses it for the backfill; it is
   deliberately built to be re-called later by the inline-text-understanding feature.

## Domain model

- `QuantityUnit` enum (string-serialized) with the nine units above.
- Static unit metadata: `Dimension` + base-factor per unit (Mass→g, Volume→ml; Count→none).
- `Quantity` readonly record struct: `decimal Value`, `QuantityUnit Unit`.
  - `Quantity.Create(value, unit)` → `Result<Quantity>`; validates `Value > 0` and finite.
  - `Quantity.TryParse(string)` → best-effort parse of "2kg", "500 ml", "1,5 l"/"1.5 l" (en/de
    decimal separators), bare "3" → `{3, Piece}`, recognised unit words → unit. Junk → no result.
  - Formatter → display string ("1 l", "2 bottles") for the frontend / read projections.

## Persistence & migration

- Drop `Quantity string?`; add `QuantityValue decimal?` + `QuantityUnit` (nullable) to both
  `ListItem` and `InventoryItem`. Drop `QuantityMaxLength` + its `HasMaxLength` config.
- One EF migration. Backfill uses `Quantity.TryParse` against the existing free-text column before it
  is dropped; parseable → value + unit, unparseable → null (decision 6).
- Migrations run automatically at startup via `context.Database.MigrateAsync()`.

## API / DTOs

- `QuantityDto(decimal Value, QuantityUnit Unit)` — nullable on requests and responses.
- Requests: `CreateItemRequest(string Text, QuantityDto? Quantity)`; update slices likewise
  (lists + inventories). Null `Quantity` on update still means "preserve existing".
- Responses: `ListItemResponse` / `InventoryItemResponse` carry `QuantityDto? Quantity`. The EF
  `ToProjection` builds it inline: `QuantityValue == null ? null : new QuantityDto(QuantityValue.Value, QuantityUnit.Value)`.
- Aggregate signatures: `AddItem(text, Quantity? quantity)` / `UpdateItem(...)`; the thin slice maps
  `QuantityDto` ↔ domain `Quantity`.
- Regenerate the TS client via `npm run api`.

## Frontend (picker layout B)

- `quantityFeature` (`Frigorino.Web/ClientApp/src/components/composer/features/quantityFeature.tsx`)
  changes its value type from `string` to `{ value: number; unit: QuantityUnit } | null`. The
  `Completion` type and the `ListFooter` / `InventoryFooter` handlers (`onAddItem` / `onUpdateItem`)
  carry the structured value through to the mutation body.
- Panel = numeric field + −/+ steppers + unit chips grouped by dimension (Mass / Volume / Count), with
  a **"Suggested" leading row stubbed empty in v1** — the future classification hook fills it.
- Numeric input accepts `.` and `,` as decimal separator; display formats per the active i18n locale.
- `ListItemContent` / `InventoryItemContent` render the formatted quantity ("1 l", "2 bottles") from
  the structured value. Existing chip / `data-testid` hooks preserved (tests assert on testids /
  `data-*`, never translated text).

## Future hooks (designed-for, not built)

- **Item classification → unit suggestions.** The picker's "Suggested" row and the unit metadata are
  the seam; a classifier can rank units per item type (milk → bottle/l).
- **Per-item container size.** A count unit can later gain an item-specific measured equivalent
  ("milk → bottle = 1 l here"), letting "2 bottles" resolve to "2 l" for math. v1 keeps count units as
  plain labels but does not bake in assumptions that block this.
- **Inline text understanding** ("2 apples", "milk 500ml" → name + quantity). Two layers:
  *quantity-token parsing* ("500ml" → `{500, Milliliter}`) is exactly the `TryParse` building block v1
  ships; *name/quantity extraction* (deciding "apples" is the name, `2` the count) is classifier/NLP
  work, genuinely future. v1 hands that future feature a ready-made parser instead of reinventing one.

## Testing

- **Domain unit tests:** `Quantity.Create` validation; `TryParse` across mass/volume/count, en/de
  decimals, and junk→none; formatter output.
- **Aggregate tests:** Add/Update with quantity; both-or-null invariant; null-preserve-on-update.
- **Integration (Reqnroll + Playwright):** add an item with a structured quantity, assert via testids.
- **Final gate:** full `dotnet test` on the solution + `docker build`; frontend lint + tsc + prettier.

## Sequencing (why planning is deferred)

Planning is **intentionally deferred**. The user's judgment is that the **classifier feature should
ship first** — once items carry classification metadata, the quantity picker can offer item-aware unit
suggestions and the inline text-understanding path becomes viable, so the quantity work lands with a
real consumer rather than as a standalone enabling layer. Revisit (run `writing-plans`) once the
classifier (see "Promote checked list items into inventory (classifier-driven)" in `IDEAS.md`) is in
`stage`.
