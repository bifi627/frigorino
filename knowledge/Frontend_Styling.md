# Frontend Styling

Working notes on the visual conventions the project has converged on after the household-feature simplification pass. Trust this over older architecture notes — most pre-existing sx patterns predate the theme module and don't reflect current best practice.

## Theme is the source of truth

The MUI theme at `Application/Frigorino.Web/ClientApp/src/theme.ts` exports `appTheme` (registered in `main.tsx`) and a `pageContainerSx` constant. It sets:

- `palette.mode: "dark"` — single-mode app.
- `shape.borderRadius: 8` — applies to Buttons, Cards, Papers, Alerts, OutlinedInput, Menu papers, Chips.
- `components.MuiButton.styleOverrides.root.textTransform: "none"` — buttons keep sentence case.
- `responsiveFontSizes(...)` — Typography variants auto-scale across xs/sm/md/lg.

If you're tempted to override one of these inline, ask whether you should be changing the theme instead.

## Use MUI primitives, not hand-rolled Boxes

| Want                                 | Use                                                                 |
|--------------------------------------|---------------------------------------------------------------------|
| Surface with shadow                  | `<Card elevation={1}>` (or `<Paper elevation={N}>`)                 |
| Surface with border, no shadow       | `<Paper variant="outlined">`                                        |
| Popover/menu drop shadow             | `<Menu elevation={4}>` (drops manual paper boxShadow)               |
| Page outer wrapper                   | `<Container sx={pageContainerSx}>` (import from `src/theme.ts`)     |
| Section title                        | `<Typography variant="h6">` (or `h5` for page titles)               |
| Body text                            | `<Typography variant="body1" | "body2">`                            |
| Caption / chip-adjacent meta         | `<Typography variant="caption">`                                    |

The `<Box sx={{ bgcolor: "background.paper", border: 1, borderColor: "divider", boxShadow: "0 1px 3px ..." }}>` pattern is reinventing `<Paper>` — don't. Canonical example: `features/households/components/HouseholdSummaryCard.tsx` uses `<Paper variant="outlined">`.

## Use MUI size props, not manual sx

For built-in components, the `size` / `fontSize` props already encode the design system's responsive scale. Prefer them over rebuilding the same values in `sx`:

- `<Button size="large">` — padding, font size, line height.
- `<Chip size="small">` — height, label padding, font size. **Do not override `height`, `fontSize`, or `MuiChip-label` separately.**
- `<IconButton size="small">`.
- `<Icon fontSize="small" | "medium" | "large">` on MUI icon components — replaces `sx={{ fontSize: { xs: 14, sm: 16 } }}`.

## Typography variants over sx fontSize

`responsiveFontSizes(theme)` is wired in `theme.ts`, so `<Typography variant="h5">` is responsive automatically. Don't add `sx={{ fontSize: { xs: "1.25rem", sm: "1.5rem" } }}` — pick the variant that matches the role and trust the theme.

`fontWeight` inline is allowed when a specific emphasis is needed (600 for headings, 500 for medium body). Don't reach for `fontSize` overrides for the same purpose.

## Shared sx constants

Per-feature `styles.ts` is fine when a sx value is genuinely shared across 2+ files of the same feature. Per-project shared sx goes in `src/theme.ts` alongside `pageContainerSx`. Avoid speculative shared modules — wait for the 2nd consumer.

## i18n and test attributes

- UI text uses `t()` from `react-i18next`. Don't hardcode user-facing strings.
- Reqnroll/Playwright step assertions use `data-testid` or `data-*` attributes — **never translated text content**. See `Application/Frigorino.IntegrationTests/Slices/Households/Members/MemberSteps.cs` for the canonical `ToHaveAttributeAsync("data-role", ...)` pattern.
- When a UI element will be asserted on by a test, render a stable attribute alongside the translated label: `<Chip data-role={roleNames[role]} label={roleLabels[role]} />` (see `features/households/members/components/MemberListItem.tsx`).

## Anti-pattern checklist (avoid)

These are already handled by the theme — don't reintroduce them inline:

| Anti-pattern                                                            | Use instead                              |
|-------------------------------------------------------------------------|------------------------------------------|
| `sx={{ borderRadius: 2 }}`                                              | (nothing — theme handles)                |
| `sx={{ "& .MuiOutlinedInput-root": { borderRadius: 2 } }}`              | (nothing — TextField inherits)           |
| `sx={{ boxShadow: "0 1px 3px rgba(0,0,0,0.1)" }}`                       | `elevation` prop on Card/Paper/Menu      |
| `sx={{ boxShadow: "0 4px 20px rgba(0,0,0,0.1)" }}`                      | `elevation={4}`                          |
| `sx={{ fontSize: { xs: "X", sm: "Y" } }}` on Typography                 | `<Typography variant="...">`             |
| `sx={{ fontSize: { xs: 14, sm: 16 } }}` on MUI icon                     | `fontSize="small"`                       |
| `sx={{ textTransform: "none" }}` on Button                              | (nothing — global override)              |
| Custom `Chip` height / inner label `px`                                 | `size="small"`                           |
| Flat `py: 3` next to neighbours' responsive `py: { xs: 2, sm: 3 }`      | Match the responsive form                |
| Hardcoded UI strings                                                    | `t("namespace.key")`                     |
| Magic role numbers `0/1/2`                                              | `HouseholdRoleValue.{Member,Admin,Owner}`|
| Asserting on translated text in tests                                   | `ToHaveAttributeAsync` on `data-*`       |

## Cross-references

- Theme module: `Application/Frigorino.Web/ClientApp/src/theme.ts`
- Canonical pages applying these rules: `features/households/pages/CreateHouseholdPage.tsx`, `features/households/pages/ManageHouseholdPage.tsx`
- ConfirmDialog primitive: `Application/Frigorino.Web/ClientApp/src/components/dialogs/ConfirmDialog.tsx` (shared confirm shape; takes a `children` slot for richer bodies — see `DeleteHouseholdDialog.tsx`)
- Role tokens: `features/households/householdRole.ts` (`HouseholdRoleValue`, `roleNames`, `roleColors`, `useRoleLabels`)
- Test-attribute convention: `Application/Frigorino.IntegrationTests/Slices/Households/Members/MemberSteps.cs`
- Vertical slice frontend pattern: backend slice rules in `knowledge/Vertical_Slices.md`; the frontend mirror lives under `ClientApp/src/features/<area>/` with one hook file per backend slice and route files reduced to thin shells.
