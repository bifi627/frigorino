# Tech debt

Running list of known issues we've spotted but consciously deferred. Add new items as they're noticed; remove them once fixed (don't mark as done — git history covers that).

Format per item:

- **Title** — one-line hook.
- **Where:** file path(s) / line refs.
- **Why deferred:** the reason it didn't get fixed in the originating change.
- **Plan:** the fix sketch, concrete enough that a future contributor doesn't re-investigate from scratch.
- **Risk if left:** what could go wrong while it's still here.

---

- **Recipe URL import: two `SaveChangesAsync` aren't transactional** — a mid-import DB failure can leave a phantom empty recipe.
- **Where:** `Application/Frigorino.Features/Recipes/ImportRecipe.cs` (the two `await db.SaveChangesAsync(ct)` calls — first persists recipe+section for real FK ids, second persists items+link).
- **Why deferred:** Surfaced by the final whole-branch review of the URL-import MVP; user chose to ship the baseline and iterate. Genuinely rare (needs a transient DB failure in the window between the two saves; the second save can't fail on validation — all data is pre-validated by the aggregate).
- **Plan:** Wrap both saves in `await using var tx = await db.Database.BeginTransactionAsync(ct); … await tx.CommitAsync(ct);`. The two-phase save is structurally required (item/link FKs need the recipe+section ids), so keep both saves — just make them atomic. Add an IT that forces the second save to fail and asserts no recipe row remains.
- **Risk if left:** On a transient DB error between the saves the endpoint returns 500 and leaves a recipe with its default section but no items/link. Low blast radius (a deletable empty recipe), but `stage` hosts a real client.
