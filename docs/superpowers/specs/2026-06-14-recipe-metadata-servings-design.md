# Recipe metadata: servings, description editing, display scaling

**Status:** Design approved (brainstorm) — ready for implementation plan
**Date:** 2026-06-14
**Branch:** `feat/recipe-metadata-servings`
**Source idea:** removed entry "Recipe metadata: servings / yield / scaling" from `IDEAS_Recipes.md`
**Predecessor spec:** `docs/superpowers/specs/2026-06-14-recipes-feature-design.md` (recipes MVP)

## Summary

Add a numeric **servings** count to recipes, make the already-existing **description** field
editable in the UI, and add a **display-only scaling control** on the recipe view that multiplies
ingredient quantities to a target serving count. No scaling persistence and no promote-to-shopping-list
wiring — those stay parked.

## Scope

In scope:
1. `Servings` (nullable int) on the `Recipe` aggregate, plumbed through create/update/response + a migration.
2. Surface the existing `Description` field as an editable input in the create and edit forms.
3. A target-servings stepper on the recipe view page that scales displayed ingredient quantities.

Out of scope (explicitly deferred):
- Scaling persistence (scale state is ephemeral view state).
- Promote-to-shopping-list integration (separate parked idea; this spec only makes scaling
  *promotion-compatible*, see Rounding).
- `YieldNote` / free-text yield (chose servings-number-only).
- Unit conversion (g↔kg, ml↔l) — phase-3 idea.

## Decisions (resolved during brainstorm)

| Decision | Choice |
|---|---|
| Yield model | Single nullable `int Servings`. No yield note. |
| Servings bounds | If provided, must be `1..99`. Null is valid (recipes without a count). |
| Scaling UX | Target-servings stepper (`[−] N [+]`), multiplier = `target / base`. |
| Scaling when servings unset | Stepper hidden entirely (no base anchor → no scaling). |
| Scaling location | Pure frontend display transform. No backend scaling logic. |
| Scale persistence | None — ephemeral local state, resets on remount. |
| Scaled-value rounding | Round to **3 decimals** to match the `numeric(12,3)` DB column. |
| Form field order | Name → Description → Servings (same form for Create + Edit). |
| Scaling cue | Stepper `[−] N [+]` with "(from N)"; scaled chips styled accent + struck-through original beside each; no banner; "Reset" text link left of the stepper, shown only while scaled. |

## Backend changes

### Domain — `Recipe` aggregate (`Frigorino.Domain/Entities/Recipe.cs`)
- Add `public int? Servings { get; set; }`.
- Add `public const int ServingsMax = 99;` (mirrors the existing `NameMaxLength`/`DescriptionMaxLength` const style).
- Extend `ValidateMetadata(name, description, servings)` (or a new `ValidateServings`): if `servings` has a
  value, it must be `>= 1` and `<= ServingsMax`, else add an `Error` with `"Property" = nameof(Servings)`.
- Thread `int? servings` into `Recipe.Create(...)` and `Recipe.Update(...)`; set `Servings` on construction/update.

### Slices
- `CreateRecipe.cs`: add `int? Servings` to `CreateRecipeRequest`; pass to `Recipe.Create`.
- `UpdateRecipe.cs`: add `int? Servings` to `UpdateRecipeRequest`; pass to `recipe.Update`.
- `RecipeResponse.cs`: add `int? Servings` to the record, to `From(...)`, and to `ToProjection`.

### Persistence
- `RecipeConfiguration.cs`: `builder.Property(r => r.Servings);` (nullable int — no extra config needed).
- New EF migration adding the nullable `Servings` column.

### Description
- No backend change — `Description` already exists end-to-end (aggregate, validation @1000 chars,
  slices, response, shown as the view-page header subtitle).

## Frontend changes

### Forms — surface Description + Servings
- Field order (both forms): **Name → Description → Servings**.
- `CreateRecipeForm.tsx`: add a **multiline** Description `TextField` (under Name) and a **number**
  Servings `TextField` (after Description). Currently it hardcodes `description: null`; replace with
  the entered values (empty servings → `null`, empty description → `null`).
- `EditRecipeForm.tsx`: same two fields in the same order, seeded once-on-mount from the loaded recipe
  (matching the existing name-seeding pattern). It currently only edits name and passes `description`
  through.
- Servings input constrained to 1..99 (reject/clamp out-of-range; empty allowed).
- Run `npm run api` after the DTO change so the generated client carries `description` + `servings`.

### Scaling control — recipe view (`RecipeViewPage.tsx`)
- Ephemeral local state `targetServings`, seeded from `recipe.servings`. No URL param, no persistence.
- Render a servings stepper row beneath the header **only when `recipe.servings` is set**:
  - Left: label `Servings (from {base})`.
  - Right: a `Reset` text link (shown **only while scaled**, i.e. `targetServings !== base`) immediately
    left of the stepper, then the stepper `[−] N [+]` (min 1, max 99).
  - `Reset` sets `targetServings` back to `base`.
- `multiplier = targetServings / recipe.servings` (1 when unchanged → no-op).
- Pass the multiplier down `RecipeContainer → RecipeItemContent`.

### Quantity scaling util (`components/composer/features/quantityFormat.ts`)
- Add `scaleQuantity(q: QuantityDto, multiplier: number): QuantityDto` — returns a copy with
  `value` multiplied, **rounded to 3 decimals, trailing zeros trimmed**. Unit unchanged (no conversion).
- `RecipeItemContent.tsx` applies `scaleQuantity` to `item.quantity` before passing to
  `ItemQuantityChip` when a multiplier ≠ 1 is active. Items without a quantity render unchanged.
- "Scaled" cue (only while `multiplier !== 1`): the scaled chip is styled with the accent (e.g.
  `color="primary"` / filled-outline variant) and shows the **struck-through original value** beside
  it. The stepper's `(from {base})` label + the `Reset` link complete the "these aren't the stored
  numbers" signal. No banner.

## Rounding & promotion compatibility (the anchor)

`QuantityValue` is `numeric(12,3)` across `RecipeItem`, `ListItem`, and `InventoryItem`. The scaling
rule — **multiply → round to 3 decimals → trim trailing zeros for display** — is chosen to match that
column scale. This guarantees the scaled number shown on the recipe view is *exactly* the value that a
future promote-to-shopping-list would persist (which will multiply the same way into a `numeric(12,3)`
column). Avoids a future "the list shows a different amount than the scaled recipe did" mismatch.
×99 of a realistic ingredient amount stays within the 9 integer digits the column allows.

When the backend promotion is eventually built, it should mirror this exact rule (a domain
`Quantity.Scale(multiplier)` rounding to 3 dp) so display and persisted values stay identical.

## Testing

- **Aggregate unit tests** (`Frigorino.Test/Recipes/RecipeAggregateTests.cs`): servings validation on
  `Create` and `Update` — null OK, `0`/negative rejected, `> 99` rejected, valid value set.
- **Integration test** (`Frigorino.IntegrationTests`): create then update a recipe with `servings`,
  assert it round-trips in the API response.
- **Scaling** is frontend-only and there is no JS test runner — verify manually via dev-up / Playwright
  MCP: stepper appears only when servings is set, changing it scales the quantity chips, math rounds to
  3 dp, items without quantities are untouched.

## Non-goals recap

No scaling persistence, no promote wiring, no yield note, no unit conversion. This is metadata + an
editable description + a display-only scaler, sized to land independently.
