# Vertical Slices

Working notes on how `Frigorino.Features` is meant to grow as more of the legacy controller/service layer is migrated. Trust this document over older architecture notes — the slice migration began after `Backend_Architecture.md` was written.

## Why this project uses vertical slices

The original layout was a classic three-layer split: controllers in `Frigorino.Web`, business logic in `Frigorino.Application/Services`, DTOs in `Frigorino.Domain/DTOs`. That layout works, but every change to a single feature touches three projects, validation goes through `throw`/`catch` for control flow, and DTOs accumulate fields that only one caller actually needs.

A vertical slice replaces that with: **one feature = one file = one endpoint**. Validation lives in the domain factory and returns `Result<T>`. Each slice owns its request DTO, its response DTO, the endpoint registration, and the handler. Slices don't depend on each other.

Reference: `Application/Frigorino.Features/Households/CreateHousehold.cs` is the canonical template — the comments at the top of that file are the slice rules and override anything in this document if they ever drift.

## Anatomy of a slice

Every slice file follows this shape:

```csharp
namespace Frigorino.Features.<Area>;

// 1. Request DTO — sealed record, primary-constructor style.
public sealed record CreateXxxRequest(string Name, string? Description);

// 2. Response DTO — sealed record with a static `From()` factory. No mapping libraries.
public sealed record XxxResponse(int Id, string Name, ...) {
    public static XxxResponse From(Xxx entity, ...) => new(entity.Id, ...);
}

// 3. Endpoint registration — one extension method on IEndpointRouteBuilder.
public static class CreateXxxEndpoint
{
    public static IEndpointRouteBuilder MapCreateXxx(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/xxx", Handle)
           .RequireAuthorization()
           .WithName("CreateXxx")
           .WithTags("Xxxs")
           .Produces<XxxResponse>(StatusCodes.Status201Created)
           .ProducesValidationProblem();
        return app;
    }

    // 4. Handler — private static method. Returns Results<TSuccess, TError> union.
    private static async Task<Results<Created<XxxResponse>, ValidationProblem>> Handle(
        CreateXxxRequest request,
        ICurrentUserService currentUser,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var creation = Xxx.Create(request.Name, ...);
        if (creation.IsFailed) return creation.ToValidationProblem();

        db.Xxxs.Add(creation.Value);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created($"/api/xxx/{creation.Value.Id}", XxxResponse.From(creation.Value));
    }
}
```

### Non-negotiable rules (from `CreateHousehold.cs`)

1. One slice = one file: request DTO + endpoint registration + handler. The response DTO lives in the same file by default, but may be promoted to a folder-level file (e.g. `HouseholdResponse.cs`, `ActiveHouseholdResponse.cs`) when shared across multiple slices in the same folder.
2. DTOs are sealed records.
3. No mapping libraries (no AutoMapper). Two blessed patterns:
   - **Write slices** that have an entity in hand after a domain factory: build the response with a static `XxxResponse.From(entity, ...)` factory method.
   - **Read slices**: project directly into the response DTO inside the LINQ query. EF translates the projection to SQL — only the needed columns are fetched and no entity is tracked. The projection IS the mapping; do not materialise an entity and re-map in memory.
4. The handler is a `private static` method on the endpoint class. No separate Handler class, no MediatR.
5. Domain rules (validation, role policy, aggregate invariants) live in the domain — in a factory (`Entity.Create`) for entity construction or in an aggregate method (`aggregate.DoXxx(...)`) for mutations on existing aggregates. Domain methods return `Result<T>`. The endpoint never re-validates; on failure it dispatches by error type (see "Write vs read shape" below).
6. No thrown exceptions for expected failures. Exceptions are reserved for genuine bugs / infrastructure faults.
7. Aim for one `SaveChangesAsync` per slice via EF navigation collections.
8. `CancellationToken` is always passed through and threaded into `SaveChangesAsync(ct)`.
9. Use `TypedResults`, not `Results`. Return type is `Results<TSuccess, ValidationProblem>` (or another union).
10. `RequireAuthorization()` on protected endpoints.
11. Wire format: enums serialize as strings (`JsonStringEnumConverter` is registered globally in `Program.cs`).
12. Each slice ships with its Reqnroll/Playwright scenarios in the same change — at least one happy-path scenario per write, plus the high-leverage negative cases (auth-boundary, invariant violations). Permission-matrix combinatorics belong in unit tests in `Frigorino.Test`, not in Playwright runs.

## Write vs read shape

Slices split into two clean shapes based on whether they mutate state. The Households + Members migration (5 writes, 2 reads) settled this convention.

### Write slices route through aggregate methods

Domain rules — validation, role policy, last-Owner protection, ownership invariants — live on the aggregate, not in the handler. The handler is orchestration: optional cross-aggregate resolution, load aggregate, call method, dispatch result, persist.

```csharp
private static async Task<Results<TSuccess, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
    int id,
    XxxRequest request,
    ICurrentUserService currentUser,
    ApplicationDbContext db,
    CancellationToken ct)
{
    // 1. Optional cross-aggregate resolution (e.g. email → User in AddMember)
    
    // 2. Load aggregate. Always filter on `IsActive`. Include only what aggregate methods + response need.
    var household = await db.Households
        .Include(h => h.UserHouseholds)
        .FirstOrDefaultAsync(h => h.Id == id && h.IsActive, ct);
    if (household is null) return TypedResults.NotFound();
    
    // 3. Invoke aggregate method. All domain rules fire inside.
    var result = household.DoXxx(currentUser.UserId, ...);
    if (result.IsFailed)
    {
        var first = result.Errors[0];
        if (first is EntityNotFoundError) return TypedResults.NotFound();
        if (first is AccessDeniedError) return TypedResults.Forbid();
        return result.ToValidationProblem();
    }
    
    // 4. Persist + respond
    await db.SaveChangesAsync(ct);
    return TypedResults.Ok(...);
}
```

Aggregate methods return `Result<T>` (or non-generic `Result` for void mutations). The handler dispatches three error categories, all defined in `Application/Frigorino.Domain/Errors/DomainErrors.cs`:

- **`EntityNotFoundError`** — caller is not a member, target doesn't exist inside the aggregate, etc. Handler maps to `NotFound`. Treats "household exists but caller has no access" the same as "household doesn't exist" — auth-boundary info-disclosure protection. **This is the convention to remember:** new contributors instinctively reach for `Forbid` here; resist that.
- **`AccessDeniedError`** — caller IS a member but lacks the role for this action. Handler maps to `Forbid`.
- **Generic `Error` with `WithMetadata("Property", ...)`** — invariant violation (e.g. last-Owner protection, "already a member"). Handler maps to `ValidationProblem` via `ToValidationProblem()`.

Slice unions match what the aggregate can actually emit. If a write slice has no validation invariants today (e.g. `DeleteHousehold`), the union is `Results<NoContent, NotFound, ForbidHttpResult>` and the dispatch is two-armed with a defensive `throw` for unmapped error types — adding a new invariant later forces a compile-time touch on the slice.

Canonical write-via-aggregate-method reference: `Application/Frigorino.Features/Households/Members/AddMember.cs` (most complex — cross-aggregate resolution + 3 internal branches). Canonical write-via-factory reference (entity creation rather than aggregate mutation) stays at `Application/Frigorino.Features/Households/CreateHousehold.cs`.

### Read slices project directly

Reads stay handler-only. No aggregate, no domain method. Inline EF projection into the response DTO — the projection IS the mapping. EF translates it to SQL, only the needed columns are fetched, no entity is tracked.

```csharp
// Auth-boundary check (handler concern, not domain)
var hasAccess = await db.UserHouseholds.AnyAsync(uh => 
    uh.UserId == currentUser.UserId && uh.HouseholdId == id && uh.IsActive, ct);
if (!hasAccess) return TypedResults.NotFound();

var response = await db.UserHouseholds
    .Where(uh => uh.HouseholdId == id && uh.IsActive)
    .OrderByDescending(uh => uh.Role)
    .Select(uh => new MemberResponse(...))
    .ToArrayAsync(ct);
return TypedResults.Ok(response);
```

Why split this way: writes have invariants spanning multiple rows in the aggregate ("always ≥1 active Owner") and benefit from one source of truth on the entity. Reads don't mutate, so the handler-direct shape is faster (one SQL projection) and the LINQ is the contract.

Canonical read references: `Application/Frigorino.Features/Households/GetUserHouseholds.cs` (no auth boundary needed — predicate filters by user), `Application/Frigorino.Features/Households/Members/GetMembers.cs` (with `AnyAsync` auth-boundary preamble).

## Folder structure & naming

### Terminology

- A **slice** = one endpoint = one file (e.g. `CreateHousehold.cs`).
- A **folder** = a grouping of related slices (e.g. `Households/`).
- A **sub-folder** is a folder nested inside another when a sub-area emerges.

### Naming

- Folder names are nouns describing the entity or area: `Households/`, `Lists/`, `Inventories/`, `Members/`.
- Slice file names are verb+noun describing the action: `CreateHousehold.cs`, `GetUserHouseholds.cs`, `UpdateMemberRole.cs`.
- Namespaces mirror folders: `Frigorino.Features.Households`, `Frigorino.Features.Households.Members`.
- The endpoint's `WithTags(...)` value drives the generated frontend service name. Use plural for entity collections (`"Households"`), singular for singletons (`"CurrentHousehold"`).

### Recommended growth shape

```
Frigorino.Features/
  Households/
    CreateHousehold.cs              ← already migrated
    GetUserHouseholds.cs            ← future
    GetHousehold.cs
    UpdateHousehold.cs
    DeleteHousehold.cs
    HouseholdResponse.cs            ← shared by all household reads
    Members/
      GetMembers.cs
      AddMember.cs
      UpdateMemberRole.cs
      RemoveMember.cs
      MemberResponse.cs
  Me/                               ← user/session-scoped concerns
    ActiveHousehold/
      GetActiveHousehold.cs         ← already migrated
      SetActiveHousehold.cs         ← already migrated
      ActiveHouseholdResponse.cs    ← shared between the two
  Lists/
    CreateList.cs
    GetLists.cs
    UpdateList.cs
    DeleteList.cs
    Items/
      GetItems.cs
      CreateItem.cs
      UpdateItem.cs
      ReorderItems.cs
      ToggleItem.cs
  Inventories/
    CreateInventory.cs
    ...
    Items/
      ...
  Results/                          ← shared infra: Result<T> → ValidationProblem
```

## When to nest, when not to

- **Nest** when a sub-area has its own lifecycle bound to a parent and shares a URL prefix. `Members/` under `Households/` fits — the route is `/api/household/{id}/members`, members can't exist without a household, and member operations rarely change at the same time as household-level CRUD. `ActiveHousehold/` under `Me/` fits the same way: the route is `/api/me/active-household`, and the resource is scoped to the calling user.
- **Don't nest** when there's no meaningful parent. The `Households/` folder is a top-level entity area, not under anything.
- **Let the URL guide the folder hierarchy.** When the route is `/api/me/active-household`, the file goes at `Me/ActiveHousehold/SetActiveHousehold.cs`. When the route is `/api/household/{id}/members`, the file goes at `Households/Members/AddMember.cs`. Folder depth ≈ URL depth (minus the leading `/api/`).
- **Heuristic for new folders:** start flat. Only nest when a folder has ~8+ items or an obvious sub-cluster forms with its own response shape and 3+ slices. Premature trees add navigation cost without payoff.
- **One headliner per folder:** the most-common operation sits at the top, not buried. `CreateHousehold.cs` lives at `Households/CreateHousehold.cs`, not at `Households/Lifecycle/Create/CreateHousehold.cs`.

## Sharing rules

- **Shared response DTOs are fine within a folder** — e.g. `HouseholdResponse.cs` is consumed by every household-read slice. The factory pattern (`HouseholdResponse.From(entity, role)`) keeps the mapping local.
- **Shared response DTOs across folders are a smell** — if `Lists/ListResponse.cs` ends up imported by `Inventories/`, ask whether you've drawn the wrong boundary.
- **Avoid `Shared/` or `Common/` folders.** They become attractors for everything that doesn't quite fit. If something is genuinely cross-cutting (e.g. `Result<T>` → `ValidationProblem` conversion), give it a name that says what it shares: `Results/`, not `Common/`.
- **Service interfaces stay in `Frigorino.Domain/Interfaces`.** Slices consume them via DI; the interface itself isn't a slice concern.
- **Domain entities and their factories stay in `Frigorino.Domain`.** A slice calls `Household.Create(...)` — it doesn't define what a household is.

## Migration status & path forward

What's done:
- `Me/ActiveHousehold/` — Get + Set
- `Households/` CRUD — Create, GetUserHouseholds, DeleteHousehold (GET/{id} and PUT/{id} dropped as orphan API)
- `Households/Members/` — GetMembers, AddMember, RemoveMember, UpdateMemberRole (POST `/leave` dropped as orphan API)

The four write slices in `Households/` (`AddMember`, `RemoveMember`, `UpdateMemberRole`, `DeleteHousehold`) all route through aggregate methods on `Household` (`AddMember`, `RemoveMember`, `ChangeMemberRole`, `SoftDelete`). See `knowledge/Migrations/Household.md` and `knowledge/Migrations/Members.md` for slice-by-slice notes.

What's still in the legacy controller/service layer (in priority-ish order):
- `Lists*` endpoints
- `ListItems*` endpoints
- `Inventories*` endpoints
- `InventoryItems*` endpoints

Two cosmetic carry-overs from the Households migration that survive because Lists/Inventories still consume them:
- `Frigorino.Application/Extensions/HouseholdMappingExtensions.cs` — shrunk to one method (`User.ToDto()`).
- `Frigorino.Domain/DTOs/HouseholdDto.cs` — shrunk to one type (`UserDto`).

Both can be renamed when Lists/Inventories migrate, since the surviving symbols belong to those features now.

### Step zero: drop, don't migrate

Before migrating any legacy endpoint, grep `ClientApp/src` for the generated method name. If there's no hand-written consumer, drop the endpoint entirely. The Households + Members migration killed 3 orphan endpoints this way (`GET /api/household/{id}`, `PUT /api/household/{id}`, `POST /leave`). Migrating dead surface is wasted work.

### Each migration is a self-contained change that:

1. Verifies a hand-written consumer exists (else drop — see step zero).
2. Adds the slice file in `Frigorino.Features/<Area>/`.
3. For writes: adds the aggregate method on the relevant entity, returning `Result<T>` with marker error types (`EntityNotFoundError`/`AccessDeniedError`) where applicable.
4. Wires the slice in `Program.cs` next to the other `app.MapXxx()` calls.
5. Removes the corresponding controller method (or the whole controller if it's the last endpoint).
6. Regenerates the frontend client (`npm run api`) and updates the affected hook(s).
7. Adds a Reqnroll scenario covering the happy path + at least one negative case (per rule 12).

## Anti-patterns to avoid

- A `Handler` class separate from the endpoint. The slice template inlines the handler as a private static method for a reason — the indirection earns nothing.
- **Aggregate-internal logic in the slice handler.** If a write handler is doing role checks against a freshly-loaded membership row, counting active Owners, or coordinating mutations across multiple rows of the same aggregate, that logic belongs on the aggregate. Handler stays orchestration; rules live where the data lives.
- Re-validating in the endpoint after the domain factory ran. If `Household.Create()` returned `Ok`, the domain trusts it; the endpoint trusts the domain.
- Throwing `ArgumentException` / `UnauthorizedAccessException` from a slice handler. Either return `Result<T>` and convert, or return a typed result union (`Results<Ok, ForbidHttpResult>`).
- Returning `Forbid` when the caller has no membership at all. The convention is **`NotFound` is the auth boundary** — "you're not a member" is indistinguishable from "the household doesn't exist" on the wire. Use `Forbid` only when the caller IS a member but lacks the role for this specific action.
- A `Shared/` folder. See above.
- Mirroring some other project's slice structure verbatim. Internal consistency and a new contributor finding the file in <30 seconds is what matters; pick a shape and live with it.
- Renaming folders mid-migration. Pick a structure for the next 6 months and accept some imperfection — the right shape is obvious in retrospect, never in advance.

## Cross-references

- Slice template & rules: `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`
- Canonical write-via-aggregate-method: `Application/Frigorino.Features/Households/Members/AddMember.cs`
- Aggregate methods (reference): `Application/Frigorino.Domain/Entities/Household.cs` (`Create`, `AddMember`, `RemoveMember`, `ChangeMemberRole`, `SoftDelete`)
- Domain error markers: `Application/Frigorino.Domain/Errors/DomainErrors.cs`
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`
- Endpoint wiring: `Application/Frigorino.Web/Program.cs` (look for `app.MapXxx()` calls)
- Frontend regen workflow: see `feedback_api_regen_workflow.md` in the per-user memory, or follow the steps in `CLAUDE.md` under "API surface"
