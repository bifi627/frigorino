# WhatsApp-style Composer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the list/inventory input composer so the text field dominates the width — icons live inside a rounded input pill, the round send button sits outside, the discard button is gone, and action icons (attach) hide once the user starts typing.

**Architecture:** A single-file change to the composer's bottom row in `Composer.tsx`, plus a dead-translation cleanup. The text field and icons get wrapped in a flex "pill" `Box`; the existing `SendButton` stays outside it. Visibility keys off the existing feature `kind` — `action` features render only while the field is empty, `modifier` features always render. No changes to feature descriptor types or any feature file.

**Tech Stack:** React 19, MUI, TypeScript. **No JS unit-test runner exists in this repo** (per CLAUDE.md) — verification is `npm run tsc` + `npm run lint` + `npm run prettier`, the existing Playwright/Reqnroll integration tests (`dotnet test`), and a manual browser pass via the dev stack.

---

## File structure

- **Modify:** `Application/Frigorino.Web/ClientApp/src/components/composer/Composer.tsx` — restructure the bottom flex row into `[ pill: textfield + toggles + actions ] [ send ]`, gate action features on empty text, delete the discard `IconButton` and its `Delete` import.
- **Modify:** `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json` — remove the now-dead `common.discardInput` key.
- **Modify:** `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json` — remove the now-dead `common.discardInput` key.

No feature files (`commentComposerFeature.tsx`, `attachComposerFeature.tsx`, `quantityComposerFeature.tsx`) change — inline icon sizing is overridden once, from the pill container.

---

### Task 1: Restructure the composer row into a pill

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/components/composer/Composer.tsx`

- [ ] **Step 1: Remove the unused `Delete` import**

In `Composer.tsx` line 1, delete this entire line:

```tsx
import { Delete } from "@mui/icons-material";
```

(After this task `Delete` is no longer referenced anywhere in the file.)

- [ ] **Step 2: Replace the bottom row markup**

Replace the entire final `<Box>…</Box>` block — the one that currently starts at
`<Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>` (currently
~line 238) and ends with the closing `</Box>` right before `</Paper>` (currently
~line 304) — with the following. This deletes the discard `IconButton`, wraps the
text field + toggles + actions in a pill, and renders action features only when
the field is empty:

```tsx
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                <Box
                    sx={{
                        flex: 1,
                        minWidth: 0,
                        display: "flex",
                        alignItems: "center",
                        gap: 0.25,
                        pl: 1.5,
                        pr: 0.5,
                        bgcolor: "action.hover",
                        // Pill shape (≈12px). Distinct from card radius on purpose.
                        borderRadius: 3,
                        // Inline icons read as in-field adornments, not standalone
                        // 44px buttons — overrides the per-feature minWidth/minHeight.
                        "& .MuiButtonBase-root": { minWidth: 38, minHeight: 38 },
                    }}
                >
                    <ComposerTextField
                        text={text}
                        onTextChange={setText}
                        onEnter={completeText}
                        inputRef={inputRef}
                        placeholder={fieldPlaceholder}
                        disabled={disabled}
                        errorMessage={dup?.message}
                        suggestions={suggestions}
                    />

                    {modifierFeatures.map((feature) =>
                        feature.renderToggle ? (
                            <Box
                                key={feature.id}
                                className="composer-panel"
                                data-testid={`composer-toggle-${feature.id}`}
                                onMouseDown={preventInputBlur}
                            >
                                {feature.renderToggle(slotFor(feature))}
                            </Box>
                        ) : null,
                    )}

                    {!trimmed &&
                        actionFeatures.map((feature) => (
                            <Box key={feature.id} className="composer-panel">
                                {feature.renderTrigger({
                                    complete: (payload) =>
                                        completeAction(
                                            feature.id,
                                            payload as Record<string, unknown>,
                                        ),
                                    disabled,
                                })}
                            </Box>
                        ))}
                </Box>

                <SendButton
                    onClick={completeText}
                    disabled={
                        !trimmed || disabled || blocked || !modifiersValid
                    }
                    editing={isEditing}
                    duplicate={Boolean(dup)}
                />
            </Box>
```

Note: `actionFeatures`, `modifierFeatures`, `trimmed`, `completeAction`,
`slotFor`, `preventInputBlur`, `ComposerTextField`, and `SendButton` are all
already defined/imported in this file — only the markup changes.

- [ ] **Step 3: Type-check**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc`
Expected: PASS, no errors. (Confirms `Delete` removal left no dangling reference and the JSX is valid.)

- [ ] **Step 4: Lint + format**

Run (from `ClientApp/`): `npm run lint` then `npm run prettier`
Expected: lint passes; prettier reformats the edited block if needed.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/Composer.tsx
git commit -m "feat(composer): WhatsApp-style pill layout; hide actions on type, drop discard button"
```

---

### Task 2: Remove the dead `discardInput` translation key

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Delete the key from English**

In `public/locales/en/translation.json`, delete line 41 (inside the `common`
object):

```json
        "discardInput": "Discard input",
```

The preceding line (`"goBackToDashboard": …,`) keeps its trailing comma and the
following line continues the object, so the JSON stays valid.

- [ ] **Step 2: Delete the key from German**

In `public/locales/de/translation.json`, delete line 41 (inside the `common`
object):

```json
        "discardInput": "Eingabe verwerfen",
```

- [ ] **Step 3: Verify no remaining references**

Run (from repo root): `grep -rn "discardInput" Application/Frigorino.Web/ClientApp/src Application/Frigorino.Web/ClientApp/public`
Expected: no matches.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "chore(i18n): remove dead common.discardInput key"
```

---

### Task 3: Verify behaviour end-to-end

**Files:** none (verification only)

- [ ] **Step 1: Build the SPA the integration harness serves**

The integration harness serves `ClientApp/build` (not live source), so rebuild
first or new layout won't appear. Run (from `ClientApp/`): `npm run build`
Expected: PASS (`tsc -b && vite build`).

- [ ] **Step 2: Run the integration tests**

Run (from repo root): `dotnet test Application/Frigorino.IntegrationTests`
Expected: PASS. Key scenarios that exercise the composer:
- comment/quantity panel open via `composer-toggle-*` (icons still present inside the pill),
- attach a photo via `composer-attach-button` on an empty field (attach is visible while empty — matches the new rule).

If Docker/Testcontainers reports the daemon is unreachable, ask the user to start Docker Desktop rather than skipping.

- [ ] **Step 3: Manual browser pass (the net for runtime/layout bugs)**

Bring up the dev stack (`/dev-up`) and, in a list view, confirm visually:
1. Empty field: pill shows the text input plus the comment + attach icons inside it; send button dimmed/disabled to the right.
2. Start typing: the attach icon disappears, the comment icon stays, send lights up. Text field clearly has most of the width.
3. Add a comment: comment icon turns its active colour, chip + panel still open from inside the pill.
4. Edit an item: quantity + comment icons both visible inside the pill; warning-coloured border/send as today.
5. No discard/trash button anywhere.

- [ ] **Step 4: Tear down (if you brought the stack up)**

Run `/dev-down` only if the user isn't keeping the stack up.

---

## Self-review notes

- **Spec coverage:** pill layout (Task 1 Step 2), actions-hide-on-type via `!trimmed` (Task 1 Step 2), modifiers always render (Task 1 Step 2), send outside + dimmed-when-empty (`SendButton` unchanged, `disabled` on `!trimmed`), discard removed + `Delete` import removed (Task 1 Steps 1–2), dead i18n key removed (Task 2), testids preserved (`composer-toggle-*` kept, `composer-attach-button` unchanged, `autocomplete-input-*` unchanged), inline icon sizing (pill container override, Task 1 Step 2). All spec sections map to a task.
- **No new feature-file edits:** icon sizing is overridden once from the pill rather than editing three feature files (DRY).
- **Type consistency:** all identifiers referenced in the new markup (`actionFeatures`, `modifierFeatures`, `trimmed`, `completeAction`, `slotFor`, `preventInputBlur`, `ComposerTextField`, `SendButton`) already exist in `Composer.tsx` and are unchanged.
