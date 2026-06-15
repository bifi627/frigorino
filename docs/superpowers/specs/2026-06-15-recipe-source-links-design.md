# Recipe source links

**Status:** Design approved (brainstorm) — ready for implementation plan
**Date:** 2026-06-15
**Branch:** `feat/recipe-source-links` (off `feat/recipe-sections`)
**Source idea:** "Source links" entry in `IDEAS_Recipes.md`
**Predecessor specs:** `2026-06-14-recipes-feature-design.md` (MVP), `2026-06-14-recipe-sections-design.md` (sections)

## Summary

A recipe usually comes from somewhere — a blog post, a YouTube video, a friend's link. This adds
an ordered list of **source links** to a recipe: each link is a required **URL** plus an optional
**display label**. Links are managed in a new static "Source links" block on the recipe edit page
(placed between Details and the ingredient-section accordion) and shown as clickable external links
on the read-only view page.

This is the cheapest slice of "hold the recipe's source material" and the eventual seed for an
"AI reads the source" path — but **this MVP does no AI and no blob/file storage**. Links are plain
rows. The feature mirrors the existing `RecipeSection` machinery (soft-delete + undo, fractional
drag-reorder, collaborative live-refresh via the revision token), minus the section-specific
nesting/cascade rules.

## Scope

In scope:
1. `RecipeLink` entity (required `Url`, optional `Label`, fractional `Rank`, soft-delete).
2. Migration `AddRecipeLinks` (no backfill — existing recipes simply have zero links).
3. `Recipe` aggregate methods for links: add / update / remove / restore / replace-restored-rank /
   reorder. URL scheme validation (`http`/`https` only) lives in the aggregate.
4. Link CRUD + reorder + restore slices under a new `recipeLinks` route group.
5. `GetRecipeRevision` extended to fold links into the revision token (collaborative live-refresh).
6. Purge wired into `DeleteInactiveItems` (soft-deleted links, and links of purged recipes).
7. Editor: a static, collapsible "Source links" block between Details and the section accordion —
   drag-reorderable rows (label + URL + delete), inline "+ Add link" composer, debounced save.
8. View: a "Sources" block under the recipe header (above the grouped sections) rendering each link
   as a clickable `target="_blank" rel="noopener noreferrer"` anchor; hidden when there are none.

Out of scope (future phase-2 / phase-3 work, see `IDEAS_Recipes.md`):
- AI extraction of ingredients/instructions from a link's content.
- File / image / document attachments (blob storage, thumbnails).
- Per-link uniqueness / dedupe.
- Link metadata fetch (title/favicon/oEmbed previews).

---

## Data model

New flat entity `RecipeLink` (sibling of `RecipeSection`):

| Field         | Type       | Notes                                                       |
| ------------- | ---------- | ---------------------------------------------------------- |
| `Id`          | int PK     |                                                            |
| `RecipeId`    | int FK     | → `Recipe` (cascade delete)                                |
| `Url`         | string     | required; `http`/`https` only; max **2048**                |
| `Label`       | string?    | optional display text; max **255**; nullable               |
| `Rank`        | string     | fractional index for ordering                              |
| `IsActive`    | bool       | soft-delete (default true)                                 |
| `CreatedAt`   | DateTime   | auto-stamped in `SaveChangesAsync`                         |
| `UpdatedAt`   | DateTime   | auto-stamped in `SaveChangesAsync`                         |

Constants on `RecipeLink`: `UrlMaxLength = 2048`, `LabelMaxLength = 255`.

- `Recipe` gains `public ICollection<RecipeLink> Links { get; set; } = new List<RecipeLink>();`.
- EF config (mirror `RecipeSection`'s configuration): FK with cascade, `Url`/`Label` `HasMaxLength`,
  and a **partial unique index** on `(RecipeId, Rank)` filtered `WHERE "IsActive"` —
  `UX_RecipeLinks_RecipeId_Rank_Active` — matching the items/sections rank-uniqueness pattern.
- `DbSet<RecipeLink> RecipeLinks` on `ApplicationDbContext`.
- Migration `AddRecipeLinks`. **No backfill**: links are an opt-in addition; existing recipes have
  none and that is a valid state (unlike sections, where every recipe must keep ≥1).

## Domain (aggregate methods on `Recipe`)

Mirror the section methods, but **simpler** — there is no cascade and **no "keep at least one"**
rule (zero links is valid). All are collaborative (any household member; no role gate), consistent
with section/item methods.

- `Result<RecipeLink> AddLink(string url, string? label)` — validate URL + label, append rank.
- `Result<RecipeLink> UpdateLink(int linkId, string url, string? label)` — re-validate, set fields.
- `Result RemoveLink(int linkId)` — soft-delete (`IsActive=false`); undo-able.
- `Result<RecipeLink> RestoreLink(int linkId)` — reactivate; de-collide rank if a now-active sibling
  shares it (mirror the section restore's rank de-collision guard).
- `Result<RecipeLink> ReplaceRestoredLinkRank(int linkId)` — re-mint rank on 23505 (used by the
  restore slice's `RankRetry`, same as sections).
- `Result<RecipeLink> ReorderLink(int linkId, int afterLinkId)` — fractional reindex; `afterId == 0`
  means move to top (same convention as items/sections).

URL validation (in a private `ValidateLinkMetadata(url, label)` helper):
- Trim `url`; required (non-empty) → else `Error("Source link URL is required.", Property=Url)`.
- `Uri.TryCreate(url, UriKind.Absolute, out var uri)` **and** `uri.Scheme` ∈ {`http`, `https`} →
  else `Error("Source link must be a valid http(s) URL.", Property=Url)`.
- Length checks: `Url` ≤ 2048, `Label` ≤ 255 (trim label; empty → null).

The slice maps these `Property`-tagged generic errors to `ValidationProblem` (400) via the existing
`ResultExtensions` dispatch, exactly like the other recipe slices.

## API slices (`Features/Recipes/Links/`)

Mirror the `Features/Recipes/Sections/` folder one-for-one:

- `GetRecipeLinks` — `GET …/links` → `RecipeLinkResponse[]`, active links ordered by `Rank`
  (inline EF projection, handler-only read).
- `CreateRecipeLink` — `POST …/links` (body: `{ url, label }`) → 201 `RecipeLinkResponse`.
- `UpdateRecipeLink` — `PUT …/links/{linkId}` (body: `{ url, label }`).
- `DeleteRecipeLink` — `DELETE …/links/{linkId}` → 204 (soft-delete).
- `RestoreRecipeLink` — `POST …/links/{linkId}/restore` → `RecipeLinkResponse` (undo path; uses
  `RankRetry.SaveWithRetryAsync` + `ReplaceRestoredLinkRank` on 23505).
- `ReorderRecipeLink` — `PUT …/links/{linkId}/reorder` (body: `{ afterId }`).
- `RecipeLinkResponse` — `{ id, url, label, rank }` (colocated DTO).

New route group in `Program.cs`:
`var recipeLinks = app.MapGroup("/api/households/{householdId:int}/recipes/{recipeId:int}/links").RequireAuthorization().WithTags("RecipeLinks");`
with `recipeLinks.MapGetRecipeLinks()` … etc., mirroring the `recipeSections` group registration.

`GetRecipeRevision` extended: fold link `MaxAsync(UpdatedAt)` and `CountAsync()` into the existing
`maxUpdatedAt` / `count` computation so a collaborator adding/editing a link advances the revision
token and triggers the view/edit pages' live refetch (same mechanism as items/sections).

`DeleteInactiveItems` (maintenance task) extended to purge soft-deleted `RecipeLinks`, and links
belonging to recipes being purged.

Regenerate the TS client (`npm run api`) after the slices land.

## Frontend

### Hooks — `features/recipes/links/` (one-hook-per-file, spread generated options)

- `useRecipeLinks(householdId, recipeId, enabled)` — query; `enabled` guards both ids `> 0`,
  `staleTime` ~30s. Mirrors `useRecipeSections`.
- `useCreateRecipeLink` / `useUpdateRecipeLink` — arg-less mutations; `onSuccess` invalidates
  `getRecipeLinksQueryKey({ path: { householdId, recipeId } })`.
- `useDeleteRecipeLink` — optimistic remove of the link from the list cache; `onSuccess` shows an
  undo toast (`t("recipes.linkDeleted")`) calling `restoreLink.mutate({ path })`; `onSettled`
  debounced-invalidate. Mirror `useDeleteRecipeSection` (minus the items-cascade bits).
- `useRestoreRecipeLink` — `onSuccess` invalidates the links key.
- `useReorderRecipeLink` — optimistic reposition mirroring `useReorderRecipeSection`
  (`afterId === 0` = top).
- `useRecipeRevision` — add a second `useRevisionInvalidation` for the links key with the same
  `isLocalMutation` predicate (mirror the sections addition).

Create hook must reconcile the `Date.now()` temp id → real server id in `onSuccess` (per the
established optimistic-create rule) so an edit-after-add PUTs the real id.

### Edit page (`RecipeEditPage.tsx`)

A new **static** `CollapsibleSection` titled `t("recipes.sourceLinks")`, placed **between** the
Details `CollapsibleSection` and the `SortableSectionList`. Persisted-expanded via
`usePersistedExpanded("recipe-edit-section:links", false)` — **defaults collapsed**. Inside, a new
`RecipeLinksCard`/section component:

- A `SortableList` (dnd-kit, reuse the existing sortable infra) of link rows. Each row: drag handle
  (testid `recipe-link-drag-handle-{id}`), a label `TextField`, a URL `TextField`, and a delete
  `IconButton` (testid `recipe-link-delete-{id}`). Row edits debounce-save via `useUpdateRecipeLink`
  (same `latest`-ref-in-`useLayoutEffect` + timer-cleanup pattern as `RecipeSectionCard`).
- An inline "+ Add link" composer (button testid `recipe-add-link`). Because `Url` is required
  (a link cannot be created empty, unlike a section with a null name), the button reveals a local
  draft row (URL field + optional label) that **POSTs only on submit** once a valid URL is entered;
  the new row's URL field receives focus. Cancel discards the draft without a server call.
- Client-side URL hint: show an inline error on the URL field when non-empty and not a valid
  http(s) URL (`t("recipes.invalidUrl")`); the server remains the source of truth (400 on save).

The block renders even with zero links (it's the affordance to add the first one).

### View page (`RecipeViewPage.tsx` / `RecipeViewList.tsx`)

A "Sources" block rendered under the recipe header, **above** the grouped ingredient sections.
Reads `useRecipeLinks`. Each link is an MUI `<Link href={url} target="_blank"
rel="noopener noreferrer">` showing `label || url` (testid `recipe-link-{id}`). The whole block is
**hidden when there are no active links** (no empty header on the read-only view).

## Cross-cutting

### i18n (`public/locales/{en,de}/translation.json`, under `recipes`)

`sourceLinks`, `addLink`, `linkUrl`, `linkLabel`, `linkUrlPlaceholder`, `linkLabelPlaceholder`,
`deleteLink`, `linkDeleted`, `invalidUrl`. (en + de.)

### Testing

- **Unit (`Frigorino.Test`)** — `Recipe` aggregate methods: add (valid + invalid URL scheme +
  over-length), update, remove, restore (incl. rank de-collision), reorder ordering. Pure aggregate
  logic, no DB.
- **Integration (`Frigorino.IntegrationTests`, Reqnroll + Playwright + Testcontainers)** — link
  create / delete-with-undo / reorder; revision-token advances on link change; `DeleteInactiveItems`
  purge IT seeds an active + an inactive link and asserts only the active survives. Steps assert on
  **testids / data-attributes only**, never translated text. Reused step bindings that appear under
  different keywords must be **double-decorated** `[Given]`+`[When]` (this repo's Reqnroll is
  keyword-sensitive).
- No frontend JS test runner exists; UI behavior is covered by the Playwright IT.

### Verification gate

`npm run tsc` + `npm run lint` + `npm run build` + full `dotnet test Application/Frigorino.sln`
(Test + IntegrationTests) + `docker build`. Run `npm run build` before the IT (the harness serves
`ClientApp/build`).

## Decisions / defaults

- **Drag-reorder** links (full `Rank` + reorder slice), per approval — not just append order.
- **Soft-delete + undo toast**, per approval — consistent with items/sections.
- **`http`/`https` URL required**, per approval — validated in the aggregate; rendered as a clickable
  external link.
- Edit-page "Source links" block **defaults collapsed**.
- **No per-link uniqueness** — duplicate URLs are allowed.
- **No backfill** — existing recipes start with zero links (a valid state).
