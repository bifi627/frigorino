# Feature-based `knowledge/` docs — design

**Date:** 2026-06-21
**Status:** Approved design, ready for implementation plan
**Scope:** Documentation restructure only. No application code changes. Reversible.

## Problem

`knowledge/` mixes two kinds of doc with no signposting:

- **Pattern docs** (how we build): `Backend_Architecture`, `Frontend_Architecture`, `Vertical_Slices`, `Frontend_Styling`, `API_Integration`, `Testing`, `Observability`, `Performance_Optimization`.
- **Feature docs** (what we built): `Recipes`, `Push_Notifications` — and that's it. The core areas (Households, Members, Lists, Inventories) have **no living reference**; their truth survives only as point-in-time *migration trackers* under `knowledge/Migrations/` (`Household.md`, `Members.md`, `Lists.md`, `Inventory.md`).

Consequence: a feature's truth is scattered. Drafting a spec for an existing area means stitching a history tracker + slice rules in `Vertical_Slices.md` + client conventions in `API_Integration.md` + styling in `Frontend_Styling.md`. No spec/plan can say "see `knowledge/<Feature>.md`" and trust it's complete.

Additionally, `AI_Classification`, `File_Storage`, `Firebase_Auth_Setup` are neither "how we build" nor a user-facing feature — they are **shared capabilities** (services many features lean on). The current flat layout hides that distinction.

## Goal

Every feature is a single citable, self-contained, **living** reference doc. Cross-cutting pattern docs stay one-copy and are linked, not duplicated. An index makes the pattern / capability / feature split explicit. The history trackers' durable decisions are preserved in the living docs; the trackers themselves retire.

## Decisions (settled with the user)

1. **Execute now** — this is a real task ending in implementation, not just idea-refinement.
2. **Migrations/: fold durable, retire trackers** — mine each tracker's still-true decisions into the matching living feature doc, then delete the four files and the `Migrations/` directory. One home per feature; no history files left as a parallel source of truth.
3. **Granularity: split out heavy sub-areas** — one doc per top-level area, but break out a sub-area into its own file when it's genuinely large and self-contained. In practice **only `Members` splits out** (distinct concept: roles/invitations/user-resolution; heaviest tracker at 16k). Lists' Items and Inventories' Items stay as major *sections* inside their parent doc (mirrors how `Recipes.md` keeps Items/Sections/Links/Attachments inline).
4. **Index: three groups** — `README.md` categorizes docs as Pattern / Capability / Feature.

### Judgment calls (called out, accepted at design approval)

- **Me / ActiveHousehold / user-settings** → a section inside `Households.md` (active-household context is household-adjacent). `Version` is trivial — no doc. FCM-token registration already lives in `Push_Notifications.md`.
- The **small-aggregates / "Household is not a god-aggregate"** rationale (currently in `Migrations/Lists.md`) is really a statement about Household's role as multi-tenant separator. Its **canonical home is `Households.md`**; `Lists.md` and `Inventories.md` link to it rather than restating it.
- The **`Frigorino.Application` project no longer exists** fact (currently referenced from `Backend_Architecture.md:14 → Migrations/Inventory.md`) is an architecture fact, not a feature fact. Restate it **present-tense inline in `Backend_Architecture.md`** and drop the dangling `Migrations/` pointer.

## The canonical feature-doc template

Formalized from `Recipes.md` / `Push_Notifications.md` (which already approximate it), with two sections added because we're folding decision-rationale out of the trackers and the cross-feature wiring deserves a named home. Sections 5 and 6 are the additions; omit any section a given feature genuinely lacks (e.g. Config).

1. **Overview** — one paragraph: what the feature is, where it sits, "same shape as the rest" framing.
2. **Domain** (`Frigorino.Domain/Entities/`) — entity table: key fields + notes (aggregate root, factory, policy method).
3. **API surface** (`Frigorino.Features/<Area>/`) — MapGroup prefix, endpoints grouped by sub-area, auth note.
4. **Key flows** — only the non-obvious mechanics (fractional-index reorder, classification trigger, attachments pipeline, expiry projection, etc.).
5. **Key decisions & rationale** *(new)* — durable "why it's this way" content folded from the trackers, in **present tense** (not "we migrated…" but "X is its own aggregate because…"). Includes still-relevant *drop* decisions (why an endpoint is deliberately absent + reintroduce conditions).
6. **Cross-feature touchpoints** *(new)* — what it calls / is called by, by id, with the contract that's easy to forget.
7. **Frontend** (`ClientApp/src/features/<area>/`) — route shells, hooks, components, notable client mechanics.
8. **Config / flags / environment** — only if the feature has any.
9. **Links out** — to the pattern docs and capability docs it relies on.

Voice: present-tense reference, terse, table-first where a table fits (matches `Recipes.md`). No status legends, no ✅/🚧, no "Deleted"/"Deferred" bookkeeping — that's tracker shape and is exactly what we're leaving behind.

## End-state file inventory of `knowledge/`

**Pattern docs** — unchanged, stay put:
`Backend_Architecture.md`* · `Frontend_Architecture.md` · `Vertical_Slices.md` · `Frontend_Styling.md` · `API_Integration.md` · `Testing.md` · `Observability.md` · `Performance_Optimization.md`
(*one-line edit: restate the `Frigorino.Application`-deleted fact inline, drop the `Migrations/` pointer.)

**Capability docs** — unchanged, stay put:
`AI_Classification.md` · `File_Storage.md` · `Firebase_Auth_Setup.md`

**Feature docs:**
| Doc | Action | Source |
|---|---|---|
| `Recipes.md` | light edit | add sections 5–6 if missing; align to template |
| `Push_Notifications.md` | light edit | align to template (already close) |
| `Households.md` | **new** | `Migrations/Household.md` + active-household/settings/blueprints slices + Household entity |
| `Members.md` | **new** | `Migrations/Members.md` + `UserHousehold` entity |
| `Lists.md` | **new** | `Migrations/Lists.md` + List/ListItem slices (items, blueprints, promote) |
| `Inventories.md` | **new** | `Migrations/Inventory.md` + Inventory/InventoryItem slices (items, settings, per-user notifications) |

**New:** `knowledge/README.md` (the index).
**Deleted:** `knowledge/Migrations/` (all four trackers + the directory).

## Durable content to fold (per feature)

A starting inventory from reading the trackers. The writer must still confirm against current source (the trackers are point-in-time — verify line refs, endpoint lists, and sub-area details like Blueprints/Settings against the actual slices before writing).

### Households.md
- `Household` is the aggregate root and the **multi-tenant separator**; `Household.Create` factory. **Canonical home for the small-aggregates / not-a-god-aggregate rationale.**
- Live endpoints: `POST /api/household` (CreateHousehold — *the* canonical new-slice reference), `GET /api/household` (GetUserHouseholds), `DELETE /api/household/{id}` (owner-only soft-delete, loops memberships in one save).
- Durable drop decision: `GET/{id}` and `PUT/{id}` are deliberately absent (zero UI consumers) — keep the reintroduce conditions (write slice + hook + `Household.Update` together).
- Active household: `ICurrentHouseholdService` holds the active id in HTTP **session** + persists to `User.LastActiveHouseholdId`; switching mutates session, not the JWT (why session middleware is mandatory). Me/ActiveHousehold/Settings slices live here.
- User-level settings (global lead-days etc.) — verify against the settings slices + `docs/superpowers/specs/2026-06-01-user-household-settings-design.md`.
- Blueprints sub-area — inspect actual slices.

### Members.md
- `UserHousehold` join entity carrying `Role` (Owner/Admin/Member); `UserHousehold.CreateMembership` factory. Nests at `Features/Households/Members/` (URL nests, lifecycle parent-bound).
- Endpoints: GetMembers, AddMember, UpdateMemberRole, RemoveMember. `/leave` deliberately dropped (DELETE covers self-removal — role guard short-circuits when caller == target).
- Policies (the substance): Owner-protection (only an Owner changes another Owner), last-Owner guard (can't demote/remove the last active Owner), AddMember reactivation branch (inactive membership → reactivate + refresh role/JoinedAt).
- Deferred/non-goals worth recording: email **invitations** (would need a `HouseholdInvitation` entity + slice family), role-change **audit log**.

### Lists.md
- `List` is its own aggregate root; permission **creator OR Admin+**. `List.Create` factory; `List.Update`/`SoftDelete` aggregate methods (role check → `AccessDeniedError`).
- Auth-boundary helper `db.FindActiveMembershipAsync(...)` (`Households/HouseholdAccessQueries.cs`) — link to the canonical rationale in `Households.md`, don't restate.
- `ListCreatorResponse` colocated (DTO-sharing-is-a-smell).
- Items: fractional-index `Rank` ordering; checked-off items purged after 30 days by `DeleteInactiveItems`. **Classification trigger** (`IProductClassificationTrigger.OnProductReferenced`) — the easy-to-forget contract (memory `project_listitem_slice_classification_contract`).
- Sub-areas: Items, Blueprints, Promote(-to-inventory).
- Cross-feature: list items → product catalog (classification) → promote-to-inventory; `CopyRecipeToList` writes via `list.AddItem` (link to `Recipes.md`).

### Inventories.md
- `Inventory` is its own aggregate root (same shape as `List`); creator OR Admin+. Create/Update/SoftDelete.
- `TotalItems` + `ExpiringItems` projection; `ExpiringWithinDays = 7` is the single source for both the count and per-item `IsExpiring`. Items have `ExpiryDate`, **no `Status`** (differs from lists).
- `InventoryCreatorResponse` colocated.
- Per-user notifications (`UserInventoryNotificationSetting`) — **cross-reference `Push_Notifications.md`, don't duplicate** the scan/ledger detail.
- Cross-feature: promote-from-list lands items here; the expiry scan reads inventory items.

## `knowledge/README.md` index

Short. Three labelled groups, one line per doc (`[Doc](file.md) — what it is / when to cite it`):

```
# knowledge/

## Pattern docs — how we build (cited by every feature)
...
## Capability docs — shared services features lean on
...
## Feature docs — one self-contained reference per user-facing area
...
```

Plus a one-sentence note on the template shape and that feature docs are the citable unit for specs/plans.

## Migrations/ retirement + dangling-ref fixes

After folding, delete `Migrations/Household.md`, `Members.md`, `Lists.md`, `Inventory.md` and the directory. Fix the two external references that would dangle (internal tracker↔tracker links die with the files):

1. **`CLAUDE.md`** — the `knowledge/` pointer paragraph mentions "per-feature migration history under `Migrations/`". Rewrite to list the new feature docs + `README.md` as the index; drop the Migrations mention.
2. **`Backend_Architecture.md:14`** — currently "(… See `Migrations/Inventory.md`.)". Restate present-tense ("there is no `Frigorino.Application` project; its services were absorbed into slices") and remove the pointer.

(The `docs/superpowers/plans/*` hits for `Frigorino.Infrastructure/Migrations/*` are **EF Core code-migrations** — unrelated, leave alone.)

## CLAUDE.md sync + IDEAS cleanup

- Update the `knowledge/` doc-pointer sentence in `CLAUDE.md` to reflect the new feature docs and the README index (kept terse per memory `feedback_claudemd_terse`).
- Remove the IDEAS.md entry "Feature-based `knowledge/` docs that specs/plans can cite directly" (lines ~129–139) as the finishing step (memory `feedback_remove_tracking_items_when_done`).

## Execution approach

**Template-first, sequential, phased.** Sequential keeps voice/structure consistent across docs — a parallel subagent fan-out writes faster but drifts in tone and section shape, which is the exact problem we're fixing.

- **Phase 1 — Template & exemplar:** finalize the template by light-editing `Recipes.md` (and `Push_Notifications.md`) into the reference exemplar(s).
- **Phase 2 — Feature docs:** write `Households.md` → `Members.md` → `Lists.md` → `Inventories.md` against the exemplar, folding durable tracker content (verified against current source).
- **Phase 3 — Index, retirement & sync:** write `README.md`; delete `Migrations/`; fix the two dangling refs; update `CLAUDE.md`; remove the IDEAS entry; grep-verify no dangling `Migrations/` links remain.

## Non-goals

- No application code, test, or config changes.
- No new pattern or capability docs; those stay put (one inline edit to `Backend_Architecture.md` excepted).
- No `Version` doc; no separate Me/Settings doc.
- Not reviving any dropped endpoint or reopening any deferred feature (invitations, audit log, CQRS) — only *recording* those decisions where relevant.

## Verification

- `grep -r "Migrations/" knowledge/ CLAUDE.md` returns no live pointer to a deleted tracker (EF `Frigorino.Infrastructure/Migrations` refs in `docs/` are expected and fine).
- Each new feature doc has the template sections (allowing for justified omissions) and links out to the relevant pattern/capability docs.
- `README.md` lists every doc present in `knowledge/` under exactly one group.
- Spot-check each folded decision against current source so no doc asserts a stale fact.
- (Docs-only — no `dotnet test` / `docker build` / `npm` verification applies.)
