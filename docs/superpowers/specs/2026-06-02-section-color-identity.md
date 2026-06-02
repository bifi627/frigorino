# Section Color Identity — Design Note

**Date:** 2026-06-02
**Status:** Decided (palette + dashboard application). App-wide rollout deferred.

## Problem

The dashboard painted a hardcoded per-feature color (`#2196F3` blue / `#FF9800`
orange / `#4CAF50` green) onto four surfaces at once — the section icon, the `+`
button, the `›` chevron, and the per-item bullet dots. Three features × four
surfaces produced color overload, and because color was pure decoration
("which feature"), the genuinely meaningful color (the red/amber expiry chip)
did not stand out.

An earlier experiment swapping the whole app's `primary` per route was rejected
as "too colorful."

## Decision

**Direction "quiet identity" (C):** color identifies a section only through the
small **icon glyph**. All other chrome (`+`, chevron) is neutral grey, the
decorative bullet dots are removed, and red/amber stay reserved for expiry
status — so the only loud color on a card is a real warning.

**Palette (V2):** one coordinated four-color family.

| Section   | Color     | Notes |
|-----------|-----------|-------|
| Household | `#5FA86F` | green — keeps the brand identity shown in the picker |
| Lists     | `#5A92CB` | blue |
| Inventory | `#4BA1A1` | teal |
| Recipes   | `#D18A77` | warm coral |

### Rules baked into the choice

- **Semantic separation.** Section colors never reuse the red/amber/green that
  expiry chips and destructive actions depend on for meaning.
- **Inventory stays cool.** Inventory must not be amber/orange because its own
  cards carry amber/red expiry chips; a warm inventory hue would compete with
  the warning. Hence teal.
- **Recipes coral is safe** because Recipes has no expiry chips.

## Section icons

Each section has a fixed glyph, centralized in `ClientApp/src/common/sections.tsx`
as `sectionIcons` (keyed by `SectionKey`, paired with `sectionColors`):

| Section   | Icon (MUI)            |
|-----------|-----------------------|
| Household | `HomeOutlined`        |
| Lists     | `ChecklistOutlined`   |
| Inventory | `KitchenOutlined` (fridge) |
| Recipes   | `RestaurantOutlined`  |

This corrected the original dashboard mix-up where Lists used the fridge icon
and Inventory used a timer.

## Implementation

- Colors centralized in `ClientApp/src/theme.ts` as `sectionColors`
  (`household`/`lists`/`inventory`/`recipes`) + a `SectionKey` type; icons in
  `common/sections.tsx`. Any surface consumes the tokens rather than hardcoding.
- **Dashboard** (`components/dashboard/WelcomePage.tsx`): icon glyph uses the
  token over a neutral `action.hover` background; `+` and chevron use
  `text.secondary`; bullet dots removed; expiry chips unchanged.
- **Feature headers** (`components/shared/PageHeadActionBar.tsx`): an optional
  `section?: SectionKey` prop renders the section's icon (section-colored glyph
  on a neutral surface) before the title — same wayfinding cue, continued into
  the feature. Wired into Lists (list/view/edit), Inventory (list/view/edit),
  and Household management. Settings is intentionally left without (not a
  section).

## Deferred (not in this pass)

- Household switcher glyph and a future Recipes section — both purely additive
  via the existing tokens, no rework needed.
