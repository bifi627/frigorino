# Ideas

Running list of features and improvements we'd like to explore but don't *have* to do. Distinct in intent from `TECH_DEBT.md` — that file holds known issues we consciously deferred; this one holds forward-looking enhancements that came up while working on something else.

Format per item:
- **Title** — one-line hook.
- **Why:** the motivation / user need it serves.
- **Sketch:** rough implementation outline (not exhaustive — a future planning conversation will detail it).
- **Impact / cost:** what changes, rough size.

---

## Persist last-active-household per user

- **Why:** Today the active household lives only in `HttpContext.Session` (in-memory cache, browser-session cookie, 30-min idle timeout). When the user closes their browser or the server restarts, the selection is lost and the system silently falls back to the highest-role / earliest-joined household — *not* what the user last picked. For users who live in a non-default household (e.g. a Member household used more often than an Owner one), this is a daily annoyance.
- **Sketch:**
  - Add nullable column `User.LastActiveHouseholdId` (FK Households, ON DELETE SET NULL).
  - Update `CurrentHouseholdService.SetCurrentHouseholdAsync` to write both the session cache and the user column.
  - Update `CurrentHouseholdService.GetCurrentHouseholdIdAsync` lookup order: session → `User.LastActiveHouseholdId` (verify access still valid) → role-based default. Each fallback rehydrates the session.
  - Migration to add the column, no data backfill needed (NULL is fine for existing users).
- **Impact / cost:** ~1 EF migration, ~15 LOC service change, no API surface change, no frontend change. Integration test for "selection survives backend restart" would be valuable but requires Testcontainers restart support — secondary.
