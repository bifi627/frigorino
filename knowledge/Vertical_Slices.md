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

1. One slice = one file (request DTO + response DTO + endpoint registration + handler).
2. DTOs are sealed records.
3. Response DTOs expose a static `From(EntityType e, ...)` factory. No mapping libraries.
4. The handler is a `private static` method on the endpoint class. No separate Handler class, no MediatR.
5. Validation lives in the domain factory and returns `Result<T>`. Failures carry `Error`s with `WithMetadata("Property", ...)`. The endpoint never re-validates; on failure it calls `ToValidationProblem()`.
6. No thrown exceptions for expected failures. Exceptions are reserved for genuine bugs / infrastructure faults.
7. Aim for one `SaveChangesAsync` per slice via EF navigation collections.
8. `CancellationToken` is always passed through and threaded into `SaveChangesAsync(ct)`.
9. Use `TypedResults`, not `Results`. Return type is `Results<TSuccess, ValidationProblem>` (or another union).
10. `RequireAuthorization()` on protected endpoints.
11. Wire format: enums serialize as strings (`JsonStringEnumConverter` is registered globally in `Program.cs`).

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
  CurrentHousehold/                 ← peer of Households (not nested — see below)
    GetCurrentHousehold.cs          ← already migrated
    SetCurrentHousehold.cs          ← already migrated
    CurrentHouseholdResponse.cs     ← already shared between the two
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

- **Nest** when a sub-area has its own lifecycle bound to a parent and shares a URL prefix. `Members/` under `Households/` fits — the route is `/api/household/{id}/members`, members can't exist without a household, and member operations rarely change at the same time as household-level CRUD.
- **Don't nest** when the area is conceptually peer-level even if it relates to a parent entity. `CurrentHousehold/` is its own resource (`/api/currenthousehold`), is session/context state rather than entity CRUD, and would be hidden under `Households/` in a way that hurts discoverability.
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
- `POST /api/household` → `Households/CreateHousehold.cs`
- `GET /api/currenthousehold` → `CurrentHousehold/GetCurrentHousehold.cs`
- `POST /api/currenthousehold/{id}` → `CurrentHousehold/SetCurrentHousehold.cs`

What's still in the legacy controller/service layer (in priority-ish order):
- `GET /api/household` (list user households)
- `GET /api/household/{id}` (single household)
- `PUT /api/household/{id}` (update)
- `DELETE /api/household/{id}` (delete)
- `Members*` endpoints
- `Lists*` endpoints
- `ListItems*` endpoints
- `Inventories*` endpoints
- `InventoryItems*` endpoints

The reads that return `HouseholdDto` (with `MemberCount`, `Members[]`, `CreatedByUser`) need a separate decision: keep the rich shape in a new `HouseholdSummaryResponse`, or push the frontend to compose from leaner reads + a dedicated members endpoint. That's a planning conversation, not a "just migrate it" task.

Each migration is a self-contained change that:
1. Adds the slice file in `Frigorino.Features/<Area>/`.
2. Wires it in `Program.cs` next to the other `app.MapXxx()` calls.
3. Removes the corresponding controller method (or the whole controller if it's the last endpoint).
4. Regenerates the frontend client (`npm run api`) and updates the affected hook(s).
5. (Ideally) adds a Reqnroll scenario covering the happy path and one negative case.

## Anti-patterns to avoid

- A `Handler` class separate from the endpoint. The slice template inlines the handler as a private static method for a reason — the indirection earns nothing.
- Re-validating in the endpoint after the domain factory ran. If `Household.Create()` returned `Ok`, the domain trusts it; the endpoint trusts the domain.
- Throwing `ArgumentException` / `UnauthorizedAccessException` from a slice handler. Either return `Result<T>` and convert, or return a typed result union (`Results<Ok, ForbidHttpResult>`).
- A `Shared/` folder. See above.
- Mirroring some other project's slice structure verbatim. Internal consistency and a new contributor finding the file in <30 seconds is what matters; pick a shape and live with it.
- Renaming folders mid-migration. Pick a structure for the next 6 months and accept some imperfection — the right shape is obvious in retrospect, never in advance.

## Cross-references

- Slice template & rules: `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`
- Endpoint wiring: `Application/Frigorino.Web/Program.cs` (look for `app.MapXxx()` calls)
- Frontend regen workflow: see `feedback_api_regen_workflow.md` in the per-user memory, or follow the steps in `CLAUDE.md` under "API surface"
