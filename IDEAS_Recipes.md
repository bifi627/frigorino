# Recipe ideas — phase 2+

Forward-looking topics for the **Recipes** feature, parked while phase 1 (MVP) ships. The MVP scope and the rationale for cutting each of these lives in [`docs/superpowers/specs/2026-06-14-recipes-feature-design.md`](docs/superpowers/specs/2026-06-14-recipes-feature-design.md). Nothing here is decided — these are bullet points to iterate over once phase 1 is finished.

Same intent as `IDEAS.md` (forward-looking enhancements), scoped to recipes. Each entry: **Title** — hook · **Why** · **Sketch** (rough, non-binding).

---

## Promote recipe to shopping list

- **Why:** The headline reason recipes exist as ingredient lists — turn a recipe into an actual shopping List with one action ("cooking this tonight → add everything to Groceries"). The MVP recipe-item shape mirrors `ListItem` specifically to make this cheap.
- **Sketch:** Mirror the existing list→inventory promotion flow. Copy selected/all recipe items into an existing or new List. Items go through the **normal list-item create pipeline on copy**, so classification (aisle/expiry) fires *then* — which is why the MVP deliberately doesn't classify at recipe-entry time. Open questions: pick-a-subset vs all, quantity carry-over / merge with existing list items, target = existing list vs new.

## Tags (filterable categories)

- **Why:** Filter recipes by course — Entry / Main / Side / Salad / Dessert / Breakfast / Drink / Snack… Client requirement #3.
- **Sketch:** Fixed curated **multi-select enum**, stored as flat `RecipeTag` join rows (`RecipeId`, `Tag`), serialized as string names like other enums. Tag-filter chips on the recipes overview. (Free-form household tags considered and set aside in favor of the curated set — revisit only if the fixed list proves limiting.)

## Attachment-flow integration tests (deferred debt)

- **Why:** Both the image-attachments phase (PR #126) and the document-attachments phase shipped covered by unit tests + manual verification only — dedicated Reqnroll/Playwright IT scenarios for the attachment flow (upload, caption edit, reorder, delete/undo, view-page open) were deferred. Tracked here so the gap isn't forgotten.

## AI-generated cooking instructions from sources

- **Why:** Client requirement #5's end-goal — once a recipe has source material (link/document), use AI to extract in-app cooking instructions, turning the MVP "ingredient list" into a full recipe.
- **Sketch:** Big one, needs its own brainstorm. Depends on attachments landing first. Vendor-neutral per the existing `IXxx` interface convention. Out of sight until attachments + a real cooking-instruction data model exist.

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
