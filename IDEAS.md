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

## Import a recipe from a URL (and later PDF/photo)

- **Status:** Scoping done (2026-06-28) — building the **JSON-LD-only URL import MVP**: deterministic fetch+parse, **no AI**, **no `IRecipeImporter` interface** (the slice calls a plain Infrastructure service), save-then-edit, paste-URL only. The sketch below is the original superset; the deferred parts — AI fallback, share-target, cover-image fetch, instructions storage, and schema.org data-model alignment — are now split into separate follow-ups under "Recipe URL import — deferred follow-ups" in `IDEAS_Recipes.md`.
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
- **Impact / cost:** Small — a domain method change + one IT. No migration.
