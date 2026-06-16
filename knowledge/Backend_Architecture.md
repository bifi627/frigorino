# Backend Architecture

Frigorino's backend is a single .NET 10 ASP.NET Core app (`Frigorino.Web`) that hosts the API as vertical slices and serves the React SPA from `wwwroot` in production. Four projects with a strict one-directional dependency graph, enforced by project references **and** ArchUnitNET tests (`Frigorino.Test/Architecture/ArchitectureTests.cs`).

## Layers & dependency direction

| Project | Holds | Depends on |
|---|---|---|
| `Frigorino.Domain` | Entities (factories + aggregate methods), value objects (`Quantity`, product classification), service-port interfaces, `FluentResults` marker errors, `IMaintenanceTask` | nothing (no infra frameworks) |
| `Frigorino.Features` | Vertical slices — one file per endpoint (request DTO + response DTO + registration + handler colocated) | Domain |
| `Frigorino.Infrastructure` | EF Core (Postgres), Firebase + dev auth, background maintenance + queue, OpenAI classification/extraction, GCS/local file storage + image processing, FCM push | Domain |
| `Frigorino.Web` | ASP.NET host, `MapGroup` slice wiring, middleware, the `Auth`/`Demo`/`WeatherForecast` scaffold controllers | Features, Infrastructure |

ArchUnit rules pinned today: `Domain → no infrastructure frameworks`, `Infrastructure → no Web`, `Features → no Web`. (The former `Frigorino.Application` project — services + mapping extensions + an integer `SortOrderCalculator` — was deleted once the slice migration completed; its `Application_Should_Not_Depend_On_Infrastructure` rule went with it. See `Migrations/Inventory.md`.)

Infrastructure types reach the host only through DI extension methods called from `Program.cs`: `AddEntityFramework`, `AddFirebaseAuth`, `AddDevAuth`, `AddBackgroundTaskQueue`, `AddFileStorage`, `AddImageProcessing`, `AddItemClassification`, `AddQuantityExtraction`, `AddRecipeQuantityExtraction`, `AddMaintenanceServices`, `AddExpiryNotifications`.

## Where the rules live

Business logic is **not** in a service layer — it lives in the Domain:

- **Construction** → entity factories (`Entity.Create(...) → Result<Entity>`) that validate inputs and emit property-keyed errors.
- **Mutation** → aggregate methods (`aggregate.DoX(...) → Result`) that enforce invariants and role policy.
- Slice handlers dispatch the `Result`: `EntityNotFoundError → 404`, `AccessDeniedError → 403`, a generic `Error` carrying `Property` metadata → `ValidationProblem`. Reads skip the domain entirely — inline EF projection straight into the response DTO, no mapping library.

Authoritative slice shape: `Vertical_Slices.md`. Canonical files: `Features/Households/CreateHousehold.cs` (factory write — rules-as-comments header at lines 1-20), `Features/Households/Members/AddMember.cs` (aggregate write), `Domain/Errors/DomainErrors.cs` (marker errors), `Features/Results/ResultExtensions.cs` (`Result → ValidationProblem`).

## Domain model

Multi-tenant; everything hangs off `Household`.

- `User` — PK is the Firebase UID (`ExternalId`, string), synced lazily on login (see Request pipeline).
- `Household` — the tenant root. `UserHousehold` join entity carries `Role` (`HouseholdRole`: Owner/Admin/Member); membership and role checks live in `Household` aggregate methods.
- `List` / `Inventory` — **peer aggregates** to `Household`, not child collections (small-aggregates DDD — keeps `Household` from becoming a god-aggregate with write contention across the tenant). Each slice resolves the auth boundary with `Features/Households/HouseholdAccessQueries.cs` `FindActiveMembershipAsync`, then passes the returned `Role` into the aggregate for policy. Each owns its items.
- `ListItem` / `InventoryItem` — ordered by a lexicographic **fractional-index `Rank`** (`Domain/Entities/FractionalIndex.cs`), not an integer sort column. Reordering mints a key *between* neighbours, so a single row updates and no periodic compaction pass is needed.
- `Recipe` — aggregate root owning Items/Sections/Links/Attachments (`Recipes.md`).
- `Product` — per-household AI classification catalog keyed `(HouseholdId, NormalizedName)` (`AI_Classification.md`).
- Notification entities — `FcmToken`, `NotificationDispatch`, `UserInventoryNotificationSetting` (`Push_Notifications.md`).

Cross-cutting conventions are centralized in `ApplicationDbContext`: `CreatedAt`/`UpdatedAt` are auto-stamped in `SaveChangesAsync`; `IsActive` soft-delete is filtered per-slice (no global query filter). Enums are stored as int (EF default) but serialized as **string names** on the wire. New entities follow this pattern rather than stamping timestamps in handlers.

## Request pipeline (`Program.cs`)

Order matters: `UseSession` → `UseAuthentication` → `UseAuthorization`, then `MapControllers` (scaffold only) and the slice `MapGroup`s, then `UseSpa` + `MapFallbackToFile("index.html")` so unknown non-API routes fall through to React.

- **Identity** — `ICurrentUserService` reads id/email/name from the Firebase JWT claims and deliberately injects no DbContext.
- **Lazy user sync** — `JwtBearerEvents.OnTokenValidated` → `UserSync.EnsureAsync` (`Infrastructure/Auth/FirebaseAuth.cs`), gated on the JWT's `auth_time` claim via a process-static cache so it fires once per real Firebase login, not per request.
- **Household context** — `ICurrentHouseholdService` keeps the active household id in the HTTP **session** (30-min idle) and mirrors it to `User.LastActiveHouseholdId` as a durable fallback. Switching households mutates session state, not the JWT — which is why session middleware is mandatory and must run before auth.

Build-time OpenAPI generation runs the entry point under a mock server; config-requiring paths (Firebase, EF migrate) are gated behind `Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"`. At normal startup, migrations apply via `context.Database.MigrateAsync()`.

## Background work

Two in-process mechanisms, both chosen to survive Railway's serverless sleep — no wall-clock scheduler, no always-on poller (the Hangfire rejection is recorded in IDEAS.md):

- **Startup maintenance batch** — `MaintenanceHostedService` waits a few seconds after boot, then runs every registered `IMaintenanceTask` once in its own DI scope (per-task errors logged, never crash startup). It re-runs on every cold start — cheap and idempotent. Today: `DeleteInactiveItems` (purges soft-deleted households/lists/inventories/items + checked list items past 30 days), `ReclaimOrphanBlobs` (`File_Storage.md`), and `BackfillProductClassification` (registered only when AI is enabled). Add one by implementing `IMaintenanceTask` and registering it in `AddMaintenanceServices`.
- **Request-triggered queue** — `BackgroundTaskQueue`, a bounded `System.Threading.Channels` queue drained by a single `QueuedHostedService` consumer (event-driven, no idle polling). It carries the AI classification + quantity-extraction jobs. Best-effort only (lost on restart), so genuinely durable work — the expiry scan — runs synchronously in-request behind a key-guarded endpoint instead (`Push_Notifications.md`).

## See also

`Vertical_Slices.md` (slice anatomy + anti-patterns), `API_Integration.md`, `Firebase_Auth_Setup.md`, `Observability.md`, `Performance_Optimization.md`; the per-feature notes `Recipes.md` / `AI_Classification.md` / `File_Storage.md` / `Push_Notifications.md`; and the completed-migration history under `Migrations/`.
