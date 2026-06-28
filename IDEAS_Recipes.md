# Recipe ideas — phase 2+

Forward-looking topics for the **Recipes** feature, parked while phase 1 (MVP) ships. The MVP scope and the rationale for cutting each of these lives in [`docs/superpowers/specs/2026-06-14-recipes-feature-design.md`](docs/superpowers/specs/2026-06-14-recipes-feature-design.md). Nothing here is decided — these are bullet points to iterate over once phase 1 is finished.

Same intent as `IDEAS.md` (forward-looking enhancements), scoped to recipes. Each entry: **Title** — hook · **Why** · **Sketch** (rough, non-binding).

---

## Tags (filterable categories)

- **Why:** Filter recipes by course — Entry / Main / Side / Salad / Dessert / Breakfast / Drink / Snack… Client requirement #3.
- **Sketch:** Fixed curated **multi-select enum**, stored as flat `RecipeTag` join rows (`RecipeId`, `Tag`), serialized as string names like other enums. Tag-filter chips on the recipes overview. (Free-form household tags considered and set aside in favor of the curated set — revisit only if the fixed list proves limiting.)

## AI-generated cooking instructions from sources

- **Why:** Client requirement #5's end-goal — once a recipe has source material (link/document), use AI to extract in-app cooking instructions, turning the MVP "ingredient list" into a full recipe.
- **Sketch:** Big one, needs its own brainstorm. Depends on attachments landing first. Vendor-neutral per the existing `IXxx` interface convention. Out of sight until attachments + a real cooking-instruction data model exist.

# Recipe URL import — deferred follow-ups

Split off while scoping the **URL import MVP** (JSON-LD-only, deterministic, no AI; save-then-edit; paste-URL only — see `IDEAS.md` → "Import a recipe from a URL" and the import design spec). Each is its own later track, parked so the MVP stays small.

## AI fallback for URL import

- **Why:** The MVP only reads sites that embed `schema.org/Recipe` JSON-LD. JS-rendered, paywalled, or unstructured pages just return "couldn't read this page." An LLM fallback covers that long tail.
- **Sketch:** The second rung of the import ladder — when the JSON-LD parse finds nothing, send the fetched page text to an LLM that returns the same `ImportedRecipe` shape the deterministic path produces. This is where the vendor-neutral ceremony the MVP deliberately skipped earns its keep: an `IRecipeImporter` port + `Null` impl + `Ai:RecipeImporter:Enabled`/`:Model` config, mirroring `AddItemClassification`/`AddQuantityExtraction`. Foreground call (user waits); hallucinated-quantity risk is contained because import already drops the user on the edit page to review.
- **Impact / cost:** Moderate. New port + OpenAI adapter + config; reuses the MVP's hardened fetch path and the slice's domain mapping.
- **Caveat — doesn't cover bot-blocked sites:** some sites drop server-side fetches entirely (DataDome and similar — e.g. `kaufland.de` returned no body at all during MVP testing), so neither JSON-LD nor an LLM fallback helps: there's no HTML to read. Those need **headless rendering** (a separate, heavier track), not this.

## PWA share-target for recipe import

- **Why:** The mobile-native entry point — from a recipe page in the phone browser (or another app's share sheet), tap Share → Frigorino → it imports. Far smoother than copy-pasting a URL.
- **Sketch:** Add a `share_target` entry to the web manifest (GET with `url`/`text`/`title` params) + a receiver route that reads the shared URL and fires the existing import endpoint. The push-only SW needs no fetch handling for a GET target. Mind the [[Railway VITE_ build args]] rule if any build-arg is involved.
- **Impact / cost:** Small once the import engine exists — manifest entry + one route + an IT.

## Store cooking instructions / steps in the recipe model

- **Why:** The MVP **drops** `recipeInstructions` (link-only) because a recipe has nowhere to store the method — it's an ingredient list today. Keeping the steps in-app is the obvious next step. Distinct from [[AI-generated cooking instructions from sources]] (that's *generate when missing*; this is *store what the import already hands us*).
- **Sketch:** Add a structured steps representation — a `RecipeStep` child with a rank (mirroring sections/items) or an ordered instructions field — consumed by import **and** manual entry, with view/edit UI on the recipe sheet. Flat schema, no entity inheritance. Needs its own brainstorm; touches the data model, the edit page, and the view page.
- **Impact / cost:** Real feature (model + migration + UI), not a quick add. A concrete piece of [[Align the recipe data model to schema.org/Recipe]].

## Align the recipe data model to schema.org/Recipe

- **Why:** We adopted `schema.org/Recipe` as the import source but map only a subset (name, description, yield, ingredients). Now that the standard is in play, decide how closely the model should track it — closer alignment makes import lossless and could enable JSON-LD **export** (portable/shareable recipes, SEO if recipes ever go public).
- **Sketch:** Evaluate adopting more standard fields: `recipeInstructions` as `HowToStep`/`HowToSection` (see [[Store cooking instructions / steps in the recipe model]]), prep/cook/total time, richer `recipeYield`, ingredient grouping, `recipeCategory`/`keywords` ↔ the existing tag vocabulary, nutrition (heavy — needs an external data source). Don't adopt wholesale — pick the fields that serve the waste-reduction mission; keep the flat schema.
- **Impact / cost:** Umbrella/strategic — decompose per field. Several migrations + UI over time.

# Phase 3 — directional bets

Bigger-picture directions, not features. The strategic thesis: phase 1–2 build the recipe *entity*; **phase 3 is where recipes become the hub that connects inventory + expiry + lists**, exploiting assets a generic recipe app doesn't have. These are theme-level — revisit and decompose when phase 2 is in hand, don't plan from here.

## Direction A — The waste-reduction loop (highest strategic fit)

Recipes as the link between "what we have" and "what we eat" — the app's actual mission (reduce household food waste).

- **"What can I cook right now?"** — match a recipe's ingredients against current inventory ("8 of 10 ingredients on hand"); rank recipes by cookability. Leans on the shared `Product` catalog + name normalization that already matches items↔products.
- **"Cook what's expiring"** — surface recipes using inventory items nearing expiry. The killer feature for the app's purpose. Leans on inventory expiry data + the existing calendar.
- **"I cooked this" → auto-decrement inventory** — marking a recipe cooked deducts its (quantity-aware) ingredients from inventory. Closes the loop: cook → inventory drops → expiry stays accurate → shopping list regenerates. Leans on the `Quantity` VO + inventory mutations.

## Direction B — Meal planning on the calendar

- **Weekly meal planner** — assign recipes to days, built on the existing inventory-expiry calendar surface.
- **Batch shopping list from the week's plan** — aggregate ingredients across planned recipes, **subtract current inventory**, merge duplicate quantities, output one List sorted by blueprint. The culmination of promote-to-list + inventory-awareness + blueprint sort.

## Direction C — Capture & intelligence

- **Recipe import** — paste a URL or use the PWA share-target (share from a recipe site/social) → AI parses ingredients + instructions into a structured recipe. Extends the phase-2 source-attachment + AI-instructions work.
- **Cooking mode** — hands-free step-by-step view, screen-wake, timers, tick ingredients as you go (once instructions exist).
- **Scaling & unit conversion** — scale servings, convert g↔kg / ml↔l, riding on the `Quantity` VO (overlaps the phase-2 servings entry).
- **Nutrition / dietary** — per-serving macros, allergen/vegetarian/vegan tags + filters, AI substitution suggestions. Heavier; needs an external nutrition data source.

## Direction D — Social / sharing (least differentiated)

- **Cross-household recipe sharing** — send a recipe to another household (respecting the multi-tenant boundary) or a read-only shareable link.
- **Household cookbook export** — PDF/print of the household's recipes.

---

**Rough priority:** A and B are the most defensible — they exploit inventory, expiry, and blueprint sorting that no generic recipe app has. C is the "feels modern" layer. D is nice-to-have. Sequencing is a future conversation; promote-to-shopping-list (phase 2) is the shared prerequisite most of A and B build on.
