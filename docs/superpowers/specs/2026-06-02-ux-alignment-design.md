# UX Alignment — Design Spec

**Date:** 2026-06-02
**Status:** Approved (design), pending spec review
**Source:** Six usability items raised by the user, consolidated into one design pass.

## Goal

Tighten the visual consistency and clarity of the Frigorino SPA. Establish a real theme (today it runs on raw MUI dark defaults), fix three concrete chip/label issues, and standardize page headers + action placement. This is the first batch of a broader look-and-feel alignment; a deeper pass (icon set, navigation paradigm) is explicitly deferred.

## Cross-cutting principles

- **Visual direction A — "subtle & semantic"** (chosen): calm neutral surfaces; brand color used sparingly; loud solid fills replaced by soft tinted banners; color reserved for *meaning* (e.g. expiry status). The clashing default pink secondary is dropped.
- **Mobile-first:** comfortable touch targets (≥ ~40px / MUI default IconButton), no hover-only affordances (every action reachable by tap), chips/labels legible at small sizes. A deeper mobile-nav rework (bottom tab bar, etc.) is **out of scope** — flagged for the future design session.
- **i18n:** every new/changed user-facing string goes through `t()` with keys added to both `en` and `de` translation files. Tests never assert on translated text.

---

## 1. Theme palette (`ClientApp/src/theme.ts`)

Today `createTheme({ palette: { mode: "dark" } })` uses MUI defaults (`primary` ≈ light blue `#90caf9`, `secondary` ≈ pink `#f48fb1`). Introduce an explicit palette so chrome (buttons, banners, primary actions) is intentional and consistent app-wide.

- **primary:** fresh green — proposed `#43A047` (main). Thematically apt for a food/fridge app; used sparingly per direction A.
- **secondary:** warm amber — proposed `#FFB300`. Replaces the pink.
- **semantic:** keep MUI `error` / `warning` / `success`; expiry coloring continues to use the existing `getExpiryColor()` thresholds (red < 2d, orange < 1w, yellow `#FFD700` < 2w, green otherwise).
- **Category accents stay as-is:** dashboard categories (lists = `#2196F3`, inventory = `#FF9800`, recipes = `#4CAF50`) remain as per-category accent colors; the palette governs chrome, not these.

Exact hex values are a starting proposal and may be tuned during implementation. Because this changes global chrome, verify a visual sweep of the main pages after applying.

> Note: green-as-primary (actions) coexists with green-as-success (expiry OK) and the green recipes accent. Acceptable because direction A uses brand color sparingly and the contexts rarely co-occur; revisit if it reads as "too much green".

## 2. Promote bar restyle (`features/lists/promote/PromoteBar.tsx`)

Replace the loud fill + pink button with a soft tinted banner:

- Container: soft tint of primary (e.g. low-opacity primary background) with a left accent border in primary; normal-weight text (not `contrastText` on a solid fill).
- Button: `variant="contained" color="primary"` (drop `color="secondary"`).
- Keep the icon, the `promote.barReady` text, the `promote-bar-review` testid, and the conditional render (`entries.length > 0`).

## 3. Shopping-list count — "open only" (#2)

**Problem:** the dashboard list rows show `${checkedCount}/${uncheckedCount}` (e.g. `6/1`) as **both** the chip label and the subtitle — confusing and duplicated. The computed date `status` is never rendered.

**Fix (`components/dashboard/WelcomePage.tsx`):**
- Chip label → **open count only**: `"{open} offen"` where open = number of unchecked (not-yet-done) items.
- Subtitle → **total**: `"{total} Artikel gesamt"`, total = checked + unchecked. (Confirm exact field names on `ListResponse`; open = unchecked items, total = all items.)
- Remove the duplicate `secondary={item.count}` rendering of the same string.
- This affects only the shopping-list collection mapping; inventory rows are handled in §4.

New translation keys (en/de), e.g. `lists.openCount` ("{{count}} offen" / "{{count}} open") and `lists.totalItems` ("{{count}} Artikel gesamt" / "{{count}} items total").

## 4. Inventory earliest-expiry chip (#3)

Show a chip on each inventory in the inventory lists with the **earliest upcoming expiry**, colored by urgency, using relative time.

**Backend (`Features/Inventories/InventoryResponse.cs` + projection):**
- Add `DateOnly? EarliestExpiryDate` (or `DateTime?` to match the item field — confirm the entity type) to the `InventoryResponse` record, the `From` factory, and the `ToProjection` expression.
- Projection: minimum `ExpiryDate` among active items that have one, e.g. `i.InventoryItems.Where(x => x.IsActive && x.ExpiryDate.HasValue).Min(x => x.ExpiryDate)`. Must stay EF-translatable (verify the `Min` over nullable translates; adjust if needed).
- `GetInventories` and `GetInventory` both use `ToProjection`, so both pick it up automatically.
- Regenerate the TS client: `npm run api` from `ClientApp/`.

**Frontend:**
- Primary target: `features/inventories/components/InventorySummaryCard.tsx` — add a chip showing relative time to `earliestExpiryDate`, colored via `getExpiryColor()`. Reuse the existing relative formatter in `utils/dateUtils.ts` (`formatExpiry` → "in X Tagen/Wochen").
- Also apply to the dashboard inventory rows (`WelcomePage.tsx`) for consistency — this requires extending the collection item shape to carry optional expiry info (date) so the generic card can render a colored chip. Keep minimal.
- **No chip** when the inventory has no items, or no items with an expiry date.

## 5. Remove strikethrough on checked items (#5)

**Decision:** no strikethrough anywhere on a checked-off item — neither the quantity chip nor the item text. The existing subtle opacity gray-out (row `opacity: 0.7` in `SortableListItem`) is the only "done" signal.

- `features/lists/items/components/ListItemContent.tsx`: remove the `textDecoration: item.status ? "line-through" : "none"` from the quantity `Chip` (lines ~56–61) — always `"none"` (i.e. drop the rule).
- Verify there is no `line-through` applied to the item **text** anywhere (Explore found none on the primary text, but confirm in `SortableListItem` / `ListItemContent` and remove if present).
- Check the inventory item row for an equivalent strikethrough and remove it too, for consistency.

## 6. Unified page header + action placement (#4 + #6)

**Decision:** every non-dashboard page uses the shared `components/shared/PageHeadActionBar.tsx`. Today only List view and Inventory view use it; the rest hand-roll headers, and User Settings has no back button at all.

**Anatomy (every page):** back button (always) · title · 0–2 inline icon actions (frequent) · overflow `⋮` menu (rare / destructive).

**Action-placement rule:**
- Frequent actions (edit, reorder/sort) → inline icon buttons in the header (max ~2).
- Rare / destructive (delete) → overflow `⋮` menu.
- **No disabled menu items:** the greyed-out "Edit" entries currently in `ListActionsMenu` / `InventoryActionsMenu` are removed; edit becomes a real inline action.

**Pages to migrate to `PageHeadActionBar`:**
- List edit — drop custom header
- Inventory edit — drop custom header
- Lists overview (`ListsPage`) — drop custom header (keep "create" as an inline header action)
- Inventories overview (`InventoriesPage`) — drop custom header (keep "create" as an inline header action)
- Manage household (`ManageHouseholdPage`) — drop custom header
- User settings (`UserSettingsPage`) — **add back button** via the shared header

Real MUI icons throughout (the brainstorm mockup used emoji as stand-ins). The icon-set refinement is deferred.

---

## Out of scope / deferred (future design session)

- Icon set / iconography refinement.
- Deeper mobile navigation paradigm (bottom tab bar, gestures).
- Any change to the dashboard's overall layout beyond the count/expiry fixes above.

## Testing & verification

- Frontend: `npm run lint`, `npm run tsc`, `npm run prettier` (write), per project convention.
- After backend DTO change: `npm run api` to regenerate the client; ensure no TS breaks.
- Backend: `dotnet test Application/Frigorino.sln` (covers unit + integration). Add/adjust a unit test for the `EarliestExpiryDate` projection if a suitable test seam exists.
- Integration tests touch testids — preserve existing testids (`promote-bar-review`, `list-item-quantity-*`, MUI Select testid conventions). Rebuild `ClientApp/build` before running IT.
- Manual mobile-first sweep of changed pages (dev-up + browser) once implemented.
- Final gate before completion: full sln test + `docker build` per project convention.

## Files touched (anticipated)

- `ClientApp/src/theme.ts` — palette.
- `ClientApp/src/features/lists/promote/PromoteBar.tsx` — restyle.
- `ClientApp/src/components/dashboard/WelcomePage.tsx` — count (open-only), inventory expiry chip.
- `ClientApp/src/features/inventories/components/InventorySummaryCard.tsx` — expiry chip.
- `ClientApp/src/features/lists/items/components/ListItemContent.tsx` — remove strikethrough.
- `ClientApp/src/features/lists/items/components/SortableListItem.tsx` (+ inventory equivalent) — confirm/remove any text strikethrough.
- `ClientApp/src/components/shared/PageHeadActionBar.tsx` — used by migrated pages (extend if a header action slot is missing).
- List edit / Inventory edit / `ListsPage` / `InventoriesPage` / `ManageHouseholdPage` / `UserSettingsPage` — migrate to shared header.
- `ListActionsMenu.tsx` / `InventoryActionsMenu.tsx` — remove disabled Edit items.
- `Features/Inventories/InventoryResponse.cs` — add `EarliestExpiryDate` to record + `From` + `ToProjection`.
- Regenerated `ClientApp/src/lib/api/*` (via `npm run api`).
- `ClientApp/public/locales/{en,de}/translation.json` — new keys.
