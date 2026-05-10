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

---

## Lightweight CQRS: query repositories + domain repositories

- **Why:** Read and write paths have different shapes but currently share `ApplicationDbContext` directly inside slices. Reads need cheap, per-feature projections to read models (no change tracking, no aggregate loading). Writes need rich domain objects that enforce invariants (`Household.Create`, future `Household.Update`, etc.) and a single `SaveChangesAsync` per slice. Splitting them lets each path stay sharp: query repos for reads, domain repos for writes. Explicitly **no MediatR** — the slice handler stays inline; we just move the EF query out of it.
- **Sketch:**
  - **Query side:** `IHouseholdQueries` in `Frigorino.Domain.Interfaces`, implementation in `Frigorino.Infrastructure.Queries.HouseholdQueries`. Methods return read models (e.g. `Task<IReadOnlyList<HouseholdListItem>>`) and use `.AsNoTracking().Select(...)` for projection. Read models live in a new `Frigorino.Domain.ReadModels` namespace (avoids the Infrastructure → Features circular reference). Slice's `Handle` injects `IHouseholdQueries` instead of `ApplicationDbContext`.
  - **Write side:** `IHouseholdRepository` in `Frigorino.Domain.Interfaces`, implementation in `Frigorino.Infrastructure.Repositories.HouseholdRepository`. Methods load aggregates (`Task<Household?> GetByIdAsync(int, ct)` with the right `Include` chain for the use case), and a `SaveChangesAsync` passthrough — or just expose the aggregate and let the slice call `db.SaveChangesAsync(ct)` once at the end (still need to think this through). `Household.Create()` / future `Household.Update()` stay on the entity.
  - **Slice rule update:** `CreateHousehold.cs:1-13` header rules + `knowledge/Vertical_Slices.md` get a new bullet: "Reads consume `IXxxQueries`. Writes consume `IXxxRepository` and call `SaveChangesAsync` once."
  - **Read model vs response DTO:** open question — collapse them (read model IS the response DTO, lives in the slice file or a shared `XxxResponse.cs`) or separate them (read model in Domain, response in Features). Collapsing is simpler; separating gives stricter Domain isolation. Lean toward collapsing initially, split only if a real reason emerges.
  - **Migration path:** introduce the pattern with one read slice as the first adopter (e.g. the next read slice after `GetUserHouseholds` — `GetHousehold` is a natural candidate since it has the rich shape that needs a real read model). Don't retroactively rewrite already-migrated slices unless touching them for unrelated reasons.
- **Impact / cost:** small per-slice (one new interface + one new query class), large in aggregate when applied across all reads. New project structure decisions (`Frigorino.Domain.ReadModels`?). Doc updates to `Vertical_Slices.md` and the `CreateHousehold.cs` header. Existing `CreateHousehold.cs` stays as-is — the write side already uses the entity factory + DbContext pattern that this idea endorses.

---

## Architecture tests to enforce dependency direction & slice isolation

- **Why:** Clean Architecture boundaries (`Web → Application → Domain`, `Web → Infrastructure → Domain`, `Application` does **not** reference `Infrastructure`) are currently enforced only by project references and reviewer vigilance. Vertical slice rules (slice handler shouldn't leak EF types into `Domain`, slices shouldn't reference each other, `Domain` stays free of `Microsoft.EntityFrameworkCore` / `Npgsql`, etc.) have no automated guard at all. As the slice count grows, accidental leaks (a slice newing up an `ApplicationDbContext` from `Infrastructure` directly, a `Domain` entity gaining an `[ForeignKey]` attribute, an `Application` service taking a Hangfire dependency) become easy to slip past review. Architecture tests catch these in CI before they land.
- **Sketch:**
  - Pick a library. Two real options today:
    - **ArchUnitNET** (TNG) — fluent, readable, actively maintained, port of the Java ArchUnit. Richer rule vocabulary (slices, cycle detection, layered architecture DSL). Heavier API surface, slightly steeper learning curve.
    - **NetArchTest.eNhancedEdition** — community-maintained fork of the original `NetArchTest` (which is dormant since 2023). Smaller, simpler fluent API. Good enough for "project X must not reference Y" + naming conventions.
    - Lean **ArchUnitNET** because the slice-isolation rules ("no slice may depend on types in another slice's folder") map naturally to its slicing DSL, which `NetArchTest` does not have a first-class concept for.
  - Add a new test project `Frigorino.ArchitectureTest` (or a single `ArchitectureTests.cs` class inside `Frigorino.Test` — decide based on whether we want it to run on every `dotnet test` or be opt-in).
  - First rule set to encode:
    - `Frigorino.Domain` may not depend on `Microsoft.EntityFrameworkCore`, `Npgsql`, `Hangfire`, `FirebaseAdmin`, `OpenAI`, ASP.NET Core types.
    - `Frigorino.Application` may not depend on `Frigorino.Infrastructure` or any of the above infra packages.
    - `Frigorino.Infrastructure` may not depend on `Frigorino.Web`.
    - Slice folders under `Frigorino.Web/Features/<Aggregate>/<SliceName>/` may not reference types in sibling slice folders.
    - Entities in `Frigorino.Domain/Entities` may not have public setters on `Id` / `CreatedAt` / `UpdatedAt` (enforce factory pattern).
  - Wire into CI by virtue of `dotnet test` already running in the existing pipeline — no extra step needed.
- **Impact / cost:** one new package reference, ~1 test class with ~5–10 rules to start. Cheap to add, pays off every time someone (or an LLM) is tempted to take a shortcut. Slight up-front cost in deciding rule wording — over-strict rules become noise, under-strict rules buy nothing. Start with the dependency-direction rules (highest signal, zero false positives) and add slice-isolation rules once we have more slices to validate against.
