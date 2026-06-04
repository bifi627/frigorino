# WhatsApp-style Composer — Design

**Date:** 2026-06-04
**Component:** `ClientApp/src/components/composer/Composer.tsx`

## Problem

A client reported that the input composer gives too much horizontal space to its
icon buttons and too little to the text field. Today the bottom row lays out
`[discard] [modifier toggles] [action triggers] [textfield(flex:1)] [send]` as
flat siblings, so on a narrow phone each 44px icon button steals width the text
field needs. We want a WhatsApp-style layout where the text field dominates.

## Goal

The text field gets nearly the full width. Icon buttons live *inside* a rounded
input pill; the round send button sits outside on the right.

## Approach (Style A — icons inside the input pill)

Restructure only the bottom flex row of `Composer.tsx`. The chip row, the
modifier panels (Collapse), the edit header, `SendButton`, suggestions, and
duplicate handling are all unchanged.

New row structure:

```
[ pill:  ComposerTextField(flex:1)  ·  modifier toggles  ·  action triggers ] [ send ]
```

- **Pill** — a rounded `Box` with a subtle neutral fill (e.g. `action.hover` /
  `grey.100`, theme-adaptive) and `borderRadius`. It contains the
  `ComposerTextField` (`flex: 1`) followed by the inline modifier-toggle and
  action-trigger icons. `flex: 1` on the pill so it fills the row beside the
  send button.
- **Outer `Paper`** — keeps its current elevation and edit/focus border
  (`primary`/`warning` border colour, `&:hover, &:focus-within` highlight).
  Padding stays so the pill has breathing room.
- **Send button** — the existing `SendButton`, a separate round button to the
  right of the pill (outside it), unchanged. When the field is empty it stays
  visible but disabled (current behaviour), matching the mockup.
- **Inline icon sizing** — the toggle/trigger icon buttons relax from
  `minWidth/minHeight: 44` to ~36–38px so they read as in-field adornments. The
  44px touch target is effectively preserved by the surrounding pill padding.

## Visibility rule (general)

Driven by feature `kind`, not per-feature hard-coding:

- **Action** features (`kind: "action"`, e.g. attach) render **only when the
  field is empty** (`!trimmed`). Attach creates a standalone media item, so it
  is irrelevant once you start typing item text — and hiding it returns that
  width to the text.
- **Modifier** features (`kind: "modifier"`, e.g. comment, quantity) **always
  render**. They augment the item being typed, so they stay available.

Future features inherit this behaviour automatically from their `kind`.

## Removed

- The discard/trash `IconButton` (current `Composer.tsx` lines ~239–257) is
  deleted entirely — confirmed unused, no test references it. Clearing typed
  text is done by normal text selection/deletion.
- The now-dead `common.discardInput` translation key in both
  `public/locales/en/translation.json` and `public/locales/de/translation.json`.
- The now-unused `Delete` icon import in `Composer.tsx`.

## Unchanged

- Comment/quantity **panels** (`Collapse`) and **chips** render above the row
  exactly as today.
- All testids: `composer-toggle-*`, `composer-panel-*`, `composer-chip-*`,
  `composer-attach-button` (and its menu/file-input testids),
  `autocomplete-input-textfield`.
- `SendButton`, `EditHeader`, duplicate handling, suggestions, focus/blur
  handling (`handleContainerClick`, `preventInputBlur`).

## Test impact

No tests expected to break:

- No test references the discard button.
- The attach integration test (`MediaItemSteps.WhenIAttachAPhoto`) clicks
  `composer-attach-button` on an **empty** field, which matches the new
  visibility rule (attach is present while the field is empty).

A manual browser pass (dev-up + Playwright/manual) should confirm: pill spacing,
the attach-hides-on-type transition, comment chip/panel still open from inside
the pill, and edit mode (quantity + comment both visible).

## Out of scope

- No backend changes.
- No change to the feature descriptor types (`ModifierFeature` / `ActionFeature`
  in `types.ts`) — the visibility rule keys off the existing `kind` field.
- No "+" expander, no collapsing-on-type for modifier icons (Style B/C rejected).
