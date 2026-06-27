# Ideas

Running list of features and improvements we'd like to explore but don't *have* to do. Distinct in intent from `TECH_DEBT.md` — that file holds known issues we consciously deferred; this one holds forward-looking enhancements that came up while working on something else.

Format per item:
- **Title** — one-line hook.
- **Why:** the motivation / user need it serves.
- **Sketch:** rough implementation outline (not exhaustive — a future planning conversation will detail it).
- **Impact / cost:** what changes, rough size.

---

## Rework user invite to household

- **Status:** placeholder — needs a dedicated brainstorming session before any sketch. This entry just captures the scenarios so they aren't lost.
- **Why:** Adding a member today (`AddMember.cs`) resolves an *existing* user and attaches them to the household. That only covers the case where the invitee already has an account and is already known. A real invite flow has to handle people who aren't users yet and needs an actual delivery mechanism so the invite reaches them.
- **Scenarios to work through:**
  1. **Invited user already exists** — current path; resolve and add (or send them an in-app/notification invite to accept rather than auto-joining?).
  2. **Invited user does not exist yet** — no `User` row to attach to. Pending-invite record keyed by email/identifier, redeemed when they sign up and first log in? Ties into [[First-run onboarding: dedicated "create your first household" page]] as a parallel onboarding entry point (invite-acceptance vs. create-first-household).
  3. **Other flows (TBD):** re-invite / revoke a pending invite, invite expiry, role chosen at invite time, declining an invite, inviting someone already in the household.
- **Open question — delivery:** how does the invitee actually receive the invite? Options to weigh later: email (needs an email sender — none wired today), shareable invite link/code, in-app notification for existing users. Keep vendor-neutral per [[Async fire-and-forget runner (in-process Channels)]] if email/push is involved (sender behind an interface).
- **Impact / cost:** unknown until brainstormed — likely a new pending-invite entity, new slices (create/accept/revoke invite), and a delivery mechanism. Defer sizing to the planning conversation.

---

## Recipe edit polish: collapse extras + compact ingredient rows

Two related refinements to the recipe edit page, best done as one pass.

- **Why:** The page over-weights the *optional* extras and under-weights the actual recipe-building. `RecipeTagSelector` (`EditRecipeForm.tsx:241`) and `RecipeSourcesStrip` links (`RecipeEditPage.tsx:265`) sit above the fold, pushing the composer + sections down. And the ingredient rows themselves still render via the shared `SortableListItem` card chrome (outlined card per item, quantity chip stacked *below* the name), so they read bulky next to the calmer new header/sections/strip. The approved prototype (`RecipeEditPrototype.tsx`, deleted in Phase 4) showed denser rows: name + small italic comment left, quantity pill right-aligned on the **same** line, hairline dividers instead of per-item cards, no per-row ⋮ menu (tap the row to edit).
- **Sketch:**
  - **(a) Collapse extras (quick win):** wrap `RecipeTagSelector` + `RecipeSourcesStrip` in a single collapsed MUI `<Accordion>` ("Details", default closed) between the name/servings block and `SortableSectionList`. No API change; tag suggestion still works behind the fold.
  - **(b) Compact rows — content (recipe-only, low risk):** restructure `RecipeItemContent.tsx` to a flex row — name (+ comment underneath) left, `ItemQuantityChip` right — instead of the current `ListItemText` primary/secondary stack. Keep testids `recipe-item-{id}`, `recipe-item-quantity-{text}`, `recipe-item-comment-{id}`.
  - **(c) Compact rows — chrome (shared — the careful part):** `SortableListItem`/`SortableList` are shared with Lists + Inventories. Add an **opt-in** `dense` prop (default false → those features unchanged) that swaps the per-item card border for a bottom hairline divider, drops the inter-row `mb`, and tightens padding. Only `RecipeContainer` passes it.
  - **(d) Menu vs tap-to-edit (the real decision):** the prototype has no per-row ⋮ menu — editing is tap-the-row. Dropping the menu removes `item-menu-button-{text}` / `edit-item-button` / `delete-item-button`, which the Reqnroll item IT drives. Either keep the menu (safe, preserves IT, ~80% of the look) or go full tap-to-edit and rewrite `ComposerSteps`/`RecipeSteps` to open the editor by clicking the row + a separate delete affordance. Pick at planning time.
- **Impact / cost:** (a) is tiny, ~1–2 frontend files. (b) is ~1 recipe-only file. (c) threads one prop across 3 shared files. Half a day total if keeping the menu; more if going menu-less (IT rewrite). No backend, no migration.

---

## Import a recipe from a URL (and later PDF/photo)

- **Why:** Creating a recipe means typing every ingredient one at a time into the composer; quantity can't even be set on entry (only via edit / async AI extraction). Bulk-paste-a-block is weak on mobile. The mobile-native answer is **import**: paste a recipe URL (or later snap/upload a page) and have the app populate the recipe for review. This is the highest-leverage fix for "recipe creation is tedious," but it's a real feature, not a quick win.
- **Sketch (headline decisions):**
  - **URL import — deterministic parse first, AI only as fallback.** Most recipe sites embed `schema.org/Recipe` JSON-LD (`<script type="application/ld+json">`) with name, servings, ingredients, and steps already structured. Fetch the page server-side → parse JSON-LD → map straight to recipe + sections + items. Only fall back to an LLM when no JSON-LD is present. Don't pay an LLM (or risk hallucinated quantities) for data the page already hands you structured — ladder: native structure beats AI.
  - **Vendor-neutral, config-gated**, mirroring the existing AI features: an `IRecipeImporter` interface with a Null impl when disabled (see `AddItemClassification` / `AddQuantityExtraction` siblings).
  - **Foreground + review, never blind-trust.** Import is a user-initiated action they wait a few seconds for; on success, drop them on the recipe **edit** page to correct before it's saved as theirs.
  - **PDF/photo** is a heavier second phase — needs PDF-text/OCR + LLM. A *photo of a cookbook page* may be more mobile-native than PDF; sequence after URL import proves out.
- **Caveats / out of scope:** Server-side URL fetch is an SSRF surface and won't handle JS-rendered or paywalled pages that lack JSON-LD. Blind auto-save (always review). Needs a dedicated brainstorm/spec before sizing.
- **Impact / cost:** Real feature. URL-via-JSON-LD MVP is moderate (one fetch+parse path, one slice, one import screen, AI fallback optional). PDF/photo is a separate, larger track. New interface; reuses existing AI config pattern; no new vendor lock-in if kept behind `IRecipeImporter`.

---

## Dedup list items on add, gated on matching unit

- **Why:** `CopyRecipeToList` and manual list-add blindly append (`List.AddItem`), so the same item piles up as duplicate rows — copy a recipe whose milk is already on the list and you get milk twice, no merge, no warning. Unlike inventory (where per-item expiry makes each purchase a deliberately distinct lot — dedup there is explicitly **not** wanted), list items have no expiry, so duplicates are pure noise.
- **Sketch:** Match on `ProductName.Normalize(name)` + unit (reuse the existing normalizer already used in `ToggleItemStatus.cs:74` — no new code). Merge rule: same name **and** same unit (or both unitless) → sum quantities; different unit, or one has a qty and the other doesn't → **add separate** (never guess a conversion — this is what sidesteps the whole unit-conversion problem). Centralize in `List.AddItem` so both copy-to-list and manual add benefit.
- **Open decisions:** (a) silent merge + informative toast ("merged into milk, now 2 L") vs. ask first — silent surprises people ("why did the count change instead of adding a row?"); lean silent + toast for v1. (b) scope: copy-to-list only, or manual add too.
- **Impact / cost:** Small — a domain method change + one IT. No migration. Pairs naturally with the toast work below.

---

## Toast placement + follow-through actions

- **Why:** `<Toaster>` (`main.tsx:136`) sets no `position`, so sonner defaults to the **bottom** — directly over the fixed composer footer on recipe/list pages, so toasts overlap the input. Separately, success toasts dead-end: "Added 6 to Groceries" closes the sheet and leaves you on the recipe with no way to jump to what you just built.
- **Sketch:** Set `position="top-center"` (robust regardless of composer height — a bottom `offset` is brittle because the composer grows when quantity/comment panels open; just verify no collision with the top AppBar). Add contextual actions via sonner `action: { label, onClick }` — copy-to-list → "View list"; promote success → "View pantry"; and undo on copy-to-list for parity with list-item delete. The pattern already exists (the `undo-action-button` class in the current `toastOptions`).
- **Impact / cost:** Tiny — frontend only, native sonner props, no new dependency. Reversible.

---

## Expire promote-to-inventory candidacy after ~7 days

- **Why:** A checked perishable becomes a promote candidate and stays one until it's resolved or purged at 30 days. Households that don't use promote-to-inventory see the `PromoteBar` count only ever climb — it "fills up over a couple weeks." A short candidacy window suppresses the nag without touching the check-off itself.
- **Sketch:** Candidacy = `IsActive && Status && PromotionExpiryHandling != null && PromotionResolvedAt == null`. Add `&& <checkedAt> >= today.AddDays(-PromoteWindowDays)` (const 7) at **read time only** — no clearing job; the item stays checked, just stops being a candidate, and the existing 30-day purge sweeps it. Hardcode the const; make it a household setting only if asked.
- **Open decision — which timestamp:** there's no dedicated "checked-at" field today. (a) Reuse `UpdatedAt` — zero migration, but it moves on *any* edit, so editing a checked item resets the 7-day clock (mildly wrong). (b) Add a `PromotionCandidacyAt` column stamped in `ApplyPromotionSuggestion` at check time — clean semantics, costs a migration. Since this is durable state shared across household members, lean (b) to avoid "why did the bar vanish?" confusion.
- **Smell to fix in the same change:** the candidacy predicate is copy-pasted across at least `GetPendingPromotions.cs:55`, `UpdateList.cs:71`, and likely `GetList` — adding the window clause means editing all of them in lockstep, or the bar count and the review sheet will disagree. Extract it to one shared expression.
- **Impact / cost:** Small–medium, backend only. A migration if going with (b); otherwise zero schema change. One IT for the window boundary.
