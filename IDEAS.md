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

## Strongly-typed IDs (Vogen)

- **Why:** Today `Household.Id` is `int` and `User.ExternalId` is `string`, which means slice handlers can swap `householdId` and `userId` parameters and the compiler won't notice. As the surface grows (Lists/Inventories will add `ListId`, `ListItemId`, `InventoryId`, `InventoryItemId` — all currently `int`), the parameter-swap bug surface multiplies. Modern .NET community has converged on source-generated value objects to eliminate this primitive obsession, with **Vogen** as the de facto choice (more capable than the ID-only `StronglyTypedId` library: validation, code analyzer, broader value-object generation). Source-gen means zero runtime overhead.
- **Sketch:**
  - Add `Vogen` package reference to `Frigorino.Domain` (pin exact version per the dependency rule).
  - Define ID structs in `Frigorino.Domain/Identifiers/` (or co-located with their entities — TBD):
    ```csharp
    [ValueObject<int>]
    public readonly partial struct HouseholdId;

    [ValueObject<string>]
    public readonly partial struct ExternalUserId;
    ```
  - Update entity properties to typed IDs (`Household.Id` becomes `HouseholdId`, `User.ExternalId` becomes `ExternalUserId`, `UserHousehold.UserId`/`HouseholdId` follow).
  - Configure EF Core value converters (one line per ID type) in `ApplicationDbContext.OnModelCreating`. Vogen has docs for this pattern; can also use `Vogen.EntityFrameworkCoreConverters` package if it exists at the time.
  - Update slice route bindings — minimal-API parameter binding via `ITryParse` should work for typed IDs out of the box.
  - Apply during the Lists/Inventories migration as a unifying step (introduces `ListId`, `InventoryId`, etc. alongside the existing IDs) rather than a standalone retrofit. This keeps the diff focused and gives a real reason to touch every entity.
  - **Open question:** what about user-facing IDs in URLs? Today `int` IDs leak DB sequence numbers (enumeration attacks, mild info disclosure). If we move to `Guid` IDs as part of this work, we get sequence-hiding for free. Counterargument: shorter URLs, simpler debugging. Decide before applying.
- **Impact / cost:** one new package, plus ~1 EF converter + ~1 entity update per ID type. Source generator means no runtime cost. Builds catch parameter-swap bugs that today only manifest at runtime as 404s. Higher confidence at cost of one-time conversion effort.

---

## Domain event infrastructure (EF Core SaveChangesInterceptor)

- **Why:** Today aggregate methods (`Household.AddMember`, `RemoveMember`, `ChangeMemberRole`, `SoftDelete`) succeed silently — there's no signal emitted. When audit log, in-app notifications, analytics events, or cross-aggregate side effects (e.g., "when a household is deleted, archive its Lists and Inventories") land, they will need to retrofit each aggregate method to do the publishing inline. Worse, that publishing usually wants to fire **after** the database transaction commits (otherwise an event for a never-saved row is a bug). The canonical .NET pattern for this is `SaveChangesInterceptor` capturing aggregate-emitted `IDomainEvent` lists and dispatching post-save. Wiring the infrastructure now (before there are subscribers) means future features just emit events from the aggregate without touching the slice handler or the persistence path.
- **Sketch:**
  - Add `IDomainEvent` marker interface in `Frigorino.Domain/Events/`.
  - Add abstract `AggregateRoot` in `Frigorino.Domain/Entities/` with `private List<IDomainEvent> _events`, `IReadOnlyCollection<IDomainEvent> DomainEvents`, `protected void Raise(IDomainEvent)`, and `void ClearDomainEvents()`. Make `Household` inherit it.
  - Define concrete events as sealed records: `MemberAdded(HouseholdId, ExternalUserId, HouseholdRole)`, `MemberRemoved(...)`, `MemberRoleChanged(...)`, `HouseholdSoftDeleted(...)`. Live in `Frigorino.Domain/Events/Households/`.
  - Aggregate methods append to the event list at the end of each successful branch (1 line each).
  - `PublishDomainEventsInterceptor : SaveChangesInterceptor` in `Frigorino.Infrastructure/EntityFramework/Interceptors/`. Override `SavedChangesAsync` (note: post-save, not pre-save — events fire only if the transaction succeeded). Drains `ChangeTracker.Entries<AggregateRoot>().SelectMany(e => e.Entity.DomainEvents)`, dispatches via a thin `IDomainEventDispatcher` (start with an in-process implementation: scan registered `IDomainEventHandler<TEvent>` services, invoke each).
  - Register the interceptor in `AddEntityFramework`: `options.AddInterceptors(sp.GetRequiredService<PublishDomainEventsInterceptor>())`.
  - **No subscribers initially** — the infra is "armed but unused". When the first feature wants to subscribe (e.g., audit log), it writes one `IDomainEventHandler<MemberAdded>` implementation; everything else stays untouched.
  - **Outbox upgrade path:** for cross-process / at-least-once delivery, swap the in-process dispatcher for one that writes events to an `Outbox` table inside the same transaction (move from `SavedChangesAsync` to `SavingChangesAsync` + a separate poller/worker). Out of scope for the initial introduction; document the upgrade as a follow-up if cross-process integration emerges.
- **Impact / cost:** ~3 new files (`IDomainEvent`, `AggregateRoot`, `PublishDomainEventsInterceptor`), one DI registration, ~5 lines per aggregate method to raise events, ~half-day total. Zero subscribers = zero behavior change today. Future features avoid retrofitting.
