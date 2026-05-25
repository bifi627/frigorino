# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Frigorino is a multi-tenant household management app (lists + inventories) built as a single deployable .NET 8 web application that serves a React SPA from `wwwroot` in production. In development the SPA is served by Vite and proxies API/openapi/scalar/hangfire calls to the backend.

## Repository layout

```
Application/
  Frigorino.sln
  Frigorino.Domain/          # Entities (with factories + aggregate methods), service interfaces, errors (FluentResults-based)
  Frigorino.Application/     # Pre-migration service implementations (Lists/Inventories only — being phased out)
  Frigorino.Features/        # Vertical slices: one file per endpoint, request/response DTOs colocated
  Frigorino.Infrastructure/  # EF Core (Postgres), Firebase auth, Hangfire jobs, OpenAI client
  Frigorino.Web/             # ASP.NET Core host, controllers (legacy slices), middleware, Hangfire dashboard auth, MapGroup wiring for slices
    ClientApp/               # React 19 + Vite + TanStack Router SPA
  Frigorino.Test/            # xUnit + FakeItEasy + EF InMemory; aggregate-method unit tests; ArchUnitNET layer rules
  Frigorino.IntegrationTests/# Reqnroll (BDD) + Playwright + Postgres Testcontainers — drives the SPA end-to-end
  Dockerfile                 # Multi-stage: builds backend + ClientApp, copies build → wwwroot
knowledge/                   # Longer-form architecture notes (read these before bigger changes)
```

The Clean Architecture dependency direction is enforced by project references AND ArchUnitNET tests in `Frigorino.Test/Architecture/ArchitectureTests.cs`: `Web → Application → Domain`, `Web → Features → Domain`, `Web → Infrastructure → Domain`. `Application` does not reference `Infrastructure`; `Features` does not reference `Web` — Infrastructure types are wired in via DI extension methods (`AddEntityFramework`, `AddApplicationServices`, `AddFirebaseAuth`, `AddHangfireServices`) called from `Frigorino.Web/Program.cs`.

## Common commands

Backend (run from repo root or `Application/`):
```powershell
dotnet restore Application/Frigorino.sln
dotnet build   Application/Frigorino.sln
dotnet run     --project Application/Frigorino.Web         # starts API on https://localhost:5001
dotnet test    Application/Frigorino.sln                   # runs all xUnit tests
dotnet test    Application/Frigorino.Test --filter "FullyQualifiedName~ListItemServiceSortOrderTests"
dotnet ef migrations add <Name> --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Migrations are applied automatically at startup via `context.Database.MigrateAsync()` in `Program.cs`.

Frontend (run from `Application/Frigorino.Web/ClientApp/`):
```powershell
npm install
npm run dev            # Vite dev server on https://localhost:44375 (proxies /api, /openapi, /scalar, /hangfire to :5001)
npm run build          # tsc -b && vite build → outputs to ClientApp/build (copied to wwwroot in Docker)
npm run lint           # eslint .
npm run fix            # eslint --fix && prettier --write .
npm run tsc            # type-check only
npm run api            # rebuild backend (emits ./src/lib/openapi.json via MSBuild target) + regenerate ./src/lib/api
```

Docker (full stack image, used in deployment):
```powershell
docker build -f Application/Dockerfile -t frigorino .
```
The Dockerfile builds the .NET solution and the SPA in parallel stages, then copies the SPA `build/` output into `wwwroot/` of the final image.

## Configuration

`Frigorino.Web/appsettings.json` has empty placeholders for all secrets — they MUST be supplied via user-secrets, environment variables, or `appsettings.Development.json`:
- `ConnectionStrings:Database` — Postgres connection string OR a `postgres://` URL (converted by `PostgresHelper.ConvertPostgresUrlToConnectionString`).
- `ConnectionStrings:Hangfire` — optional separate Postgres connection for Hangfire storage; falls back to `Database` when blank.
- `FirebaseSettings:ValidIssuer` / `ValidAudience` / `AccessJson` — Firebase JWT validation + service account JSON.
- `Hangfire:AdminEmail` — email claim required to access the `/hangfire` dashboard (production/staging; open in Development). Set in user-secrets or Railway env.
- `OpenAiSettings:APIKey` / `Model` — used by `ClassificationService` for article classification.

### Local dev: two modes

The dev-auth bypass is **opt-in, not a committed default**. Manual `dotnet run` / `npm run dev` keep the real Firebase flow (requires user-secrets).

- **Real Firebase + your DB** (default): `dotnet run --project Application/Frigorino.Web` + `npm run dev`. Uses your user-secrets.
- **Bypass + local Postgres** (agents / fresh-clone): `powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1` (down: same with `dev-down.ps1`). Activated by the `LocalDb` launch profile (sets `DevAuth__Enabled=true` + local conn string) and `$env:VITE_DEV_AUTH=true` set by the script before spawning vite. Identity on both sides: `dev-user` / `dev@frigorino.local`.

Bypass implementation: `DevAuthHandler` (`Frigorino.Infrastructure/Auth/DevAuthHandler.cs`, gated on `Development` env + `DevAuth:Enabled`) and `authProvider.ts` (gated on `VITE_DEV_AUTH`). Scripts log to `.dev/*.log` (gitignored); per-worktree port-based kill — ports are scanned above the user's 5001/44375/5432/8080 and recorded in .dev/stack.json, so parallel worktrees coexist and the user's fixed env is untouched; handles Windows' missing process-group cascade. `/readyz` returns 503 in bypass mode (cosmetic — `/healthz` still 200).

Agent skills wrap the scripts: `/dev-up` is auto-invokable (description gates on actual UI verification need); `/dev-down` is manual-only. Pair with Playwright MCP `--isolated` (`~/.claude.json`) for ephemeral browser profiles.

## Architecture notes

### Vertical slice architecture (in progress)

Authoritative shape: `knowledge/Vertical_Slices.md`. Trust it over older architecture notes.

The codebase is migrating from a controller + service + DTO layered shape to vertical slices. Each slice = one file = one endpoint, with request DTO + response DTO + endpoint registration + handler colocated. Domain rules (validation, role policy, aggregate invariants) live in `Frigorino.Domain` — either in entity factories (`Entity.Create`) for construction or in aggregate methods (`aggregate.DoXxx`) for mutations. Domain methods return `FluentResults.Result<T>`; the slice handler dispatches by error type (`EntityNotFoundError` → 404, `AccessDeniedError` → 403, generic `Error` with `Property` metadata → `ValidationProblem`). Reads stay handler-only — inline EF projection into the response DTO (no mapping libraries).

Canonical references:
- Write-via-factory template: `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13` (rules-as-comments header overrides `Vertical_Slices.md` when they drift).
- Write-via-aggregate-method template: `Application/Frigorino.Features/Households/Members/AddMember.cs` (most complex — cross-aggregate user resolution + 3 internal branches).
- Domain marker errors: `Application/Frigorino.Domain/Errors/DomainErrors.cs`.
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`.

Migration trackers (per-feature progress, decisions, drops): `knowledge/Migrations/Household.md`, `knowledge/Migrations/Members.md`. Current state: Households + Members are fully migrated to slices (5 writes through aggregate methods on `Household`, 2 reads via direct projection). Lists / ListItems / Inventories / InventoryItems still use the older controller (`Frigorino.Web/Controllers/`) + service (`Frigorino.Application/Services/`) pattern — queued for migration. **When adding a new endpoint, write a slice; do not extend the controllers.**

### Request pipeline (`Frigorino.Web/Program.cs`)
Order matters: `UseSession` runs before `UseAuthentication`/`UseAuthorization`. Lazy `Users`-row sync is done inside `JwtBearerEvents.OnTokenValidated` (see `Frigorino.Infrastructure/Auth/FirebaseAuth.cs`) — fires once per real Firebase login via the JWT's `auth_time` claim, not per request. `MapControllers` is followed by `UseSpa` + `MapFallbackToFile("index.html")` so unknown routes fall through to the React app.

### Multi-tenant household context
- `ICurrentUserService` resolves the user from the Firebase JWT and lazily creates a `User` row on first login.
- `ICurrentHouseholdService` keeps the active household ID in the **HTTP session** (`AddSession`, 30-min idle). Switching households mutates session state, not the JWT — this is why session middleware is mandatory.
- All household-scoped controllers/services should go through these interfaces rather than reading claims directly.
- `UserHousehold` is the join entity carrying a `Role` (Owner/Admin/Member) — permission checks live in the service layer.
- `IsActive` soft-delete and automatic `CreatedAt`/`UpdatedAt` are managed centrally in `ApplicationDbContext`. New entities should follow the same pattern instead of setting timestamps in services.

### Background jobs (Hangfire)

Hangfire (Hangfire.AspNetCore + Hangfire.PostgreSql, `schema=hangfire`, auto-created on first
run) is the durable fire-and-forget queue. Wiring lives in
`Frigorino.Infrastructure/Hangfire/HangfireDependencyInjection.cs` (`AddHangfireServices`), called
from `Program.cs` and gated off at build-time OpenAPI generation and in the `IntegrationTest`
environment (configuring Postgres storage opens a DB connection). Storage uses
`ConnectionStrings:Hangfire` when set, else falls back to `Database`; processed jobs are retained
for 7 days (`WithJobExpirationTimeout`, up from Hangfire's 24h default) for post-mortem history.

- **Queue-first, sleep-tolerant.** Railway free-tier sleeps on HTTP-idle, so no in-process
  scheduler fires while suspended. Recurring jobs are allowed ONLY with
  `MisfireHandlingMode.Relaxed` (catch up once on wake); never rely on a precise wall-clock time.
- **Producers** inject `IBackgroundJobClient` and call `Enqueue<TJob>(j => j.ExecuteAsync(...))`.
  Jobs live in `Frigorino.Infrastructure/Jobs/` as scoped classes with `ExecuteAsync(...)` and log
  via `ILogger<T>` only — an `ILogger`→Hangfire.Console bridge (in `Frigorino.Infrastructure/Hangfire/`)
  mirrors output to the dashboard's per-job console.
- The only recurring job today is `CleanupInactiveEntitiesJob` (`Cron.Daily()`), which replaced the
  former `MaintenanceHostedService` startup batch.
- The dashboard at `/hangfire` is gated by `HangfireDashboardAuthFilter`: open in Development,
  otherwise an authenticated Firebase principal whose email claim equals `Hangfire:AdminEmail`
  (the token reaches dashboard requests via the `hf_dashboard_token` cookie shim in
  `FirebaseAuth.OnMessageReceived`).

### API surface
- Endpoints come from two sources during the slice migration: vertical slices in `Frigorino.Features` are wired via `MapGroup(...).Map<SliceName>()` declarations in `Program.cs` (each group owns the route prefix + `RequireAuthorization()` + `WithTags(...)`); legacy endpoints are still in controllers under `Frigorino.Web/Controllers/`. New endpoints should be slices, not controllers — see "Vertical slice architecture" above. In Development, the spec is served at `/openapi/v1.json` and the [Scalar](https://scalar.com) UI at `/scalar/v1` (replaces the old SwaggerUI).
- `JsonStringEnumConverter` is registered globally — enums serialize as strings on the wire, and the frontend's generated client expects string enums.
- OpenAPI is generated via `Microsoft.AspNetCore.OpenApi` + `Microsoft.Extensions.ApiDescription.Server`. `dotnet build Frigorino.Web` writes `ClientApp/src/lib/openapi.json` (configured via `OpenApiDocumentsDirectory` + `OpenApiGenerateDocumentsOptions` in the csproj).
- The build-time generator runs the app entry point with a mock server. Code paths that require real config (Firebase auth, EF migrations) are gated behind `var isBuildTimeOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"` in `Program.cs`.
- The frontend client is generated by `npm run api` from `src/lib/openapi.json` via [`@hey-api/openapi-ts`](https://heyapi.dev) (config: `ClientApp/openapi-ts.config.ts`). The full workflow is one command from `ClientApp/`: change endpoints/DTOs → `npm run api` (rebuilds the backend, emits the spec, regenerates the TS client). No backend boot, no DB, no manual copy. Generated code under `src/lib/api/` is committed.

### Frontend
- TanStack Router uses **file-based routing**; `routeTree.gen.ts` is auto-generated by the `@tanstack/router-plugin/vite` plugin — do not edit by hand.
- `_protected.tsx` is the auth-gated layout; routes inside it require a Firebase user.
- Server state goes through TanStack Query. Every endpoint has generated hooks-ready helpers in `src/lib/api/@tanstack/react-query.gen.ts` (from the hey-api `@tanstack/react-query` plugin). One-hook-per-file under `features/<area>/` spreads the generated options into `useQuery`/`useMutation` — never write `queryFn`/`mutationFn`/`queryKey` by hand. See the "API hook conventions" section below. Client state uses Zustand. Do not introduce a third state layer (Redux, Context-as-store) for new features.
- Route files under `src/routes/` are thin shells: `createFileRoute` + `requireAuth` + import the page component from `features/<area>/pages/`. See `routes/household/create.tsx` for the canonical shape.
- The fetch client is configured once at app boot in `src/common/apiClient.ts` (imported for its side effect from `main.tsx`). `client.setConfig` injects Firebase ID tokens via the `auth` resolver. There is no `ClientApi` singleton — generated SDK functions import the configured `client` internally.
- i18n is wired via `i18next` + `i18next-http-backend` — translation files live under `ClientApp/public/locales/{en,de}/translation.json`. **Tests never assert on translated text** — see styling guide.

### API hook conventions

Every TanStack Query hook in `features/<area>/use*.ts` follows this exact shape (no exceptions):

**Query hook** — takes the IDs needed to build the URL, spreads `getXOptions`:
```ts
export const useList = (householdId: number, listId: number, enabled = true) =>
    useQuery({
        ...getListOptions({ path: { householdId, listId } }),
        enabled: enabled && listId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
```

**Mutation hook** — no path/id args; the caller passes the full `{ path, body }` to `mutate`. Invalidation reads from `variables.path.*` in `onSuccess`/`onSettled`:
```ts
export const useDeleteList = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...deleteListMutation(),
        onSuccess: (_data, variables) => {
            queryClient.removeQueries({ queryKey: getListQueryKey({ path: { ... } }) });
            queryClient.invalidateQueries({ queryKey: getListsQueryKey({ path: { householdId: variables.path.householdId } }) });
        },
    });
};
```

**Rules:**
- Never write `queryFn`, `mutationFn`, or manual `queryKey` arrays. Spread `getXOptions` / `xMutation` / `getXQueryKey()`.
- Never reintroduce `*Keys.ts` files — auto-generated keys (with `tags`) cover both point and tag-based invalidation. For broad invalidation across a domain, use `queryClient.invalidateQueries({ predicate: q => (q.queryKey[0] as { tags?: string[] })?.tags?.includes('Households') })`.
- Mutation hooks are arg-less; callers pass `{ path: {...}, body: ... }` to `mutate` / `mutateAsync`.
- Optimistic update hooks (toggle/reorder/create/update items) keep their `onMutate`/`onError`/`onSettled` callbacks — those are the substance the codegen doesn't replace. Read/write queryKeys via `getXQueryKey({ path: {...} })`, not literal arrays.
- The error from a mutation may be widened to `unknown` when the OpenAPI error response is `unknown` (e.g. 404 bodies). When rendering in JSX, type the local as `unknown` and use `error instanceof Error ? error.message : t("common.errorOccurred")`.
- Hey-api throws the parsed error response body on non-2xx (because the generated mutationFn passes `throwOnError: true`). `instanceof ApiError` is no longer a thing — to read field-level validation errors, narrow on the body shape: `(error as { errors?: { email?: string[] } } | null)?.errors?.email`.

### Frontend styling

Authoritative shape: `knowledge/Frontend_Styling.md`. Trust it over the inline sx patterns still present in the legacy Lists/Inventories routes.

Key rules: the theme at `ClientApp/src/theme.ts` owns `shape.borderRadius`, responsive typography, and button overrides — don't reintroduce `borderRadius: 2`, manual `boxShadow`, or `fontSize: { xs, sm }` inline. Use MUI size props (`size="small"`, `fontSize="small"`) and `<Card elevation={N}>` / `<Paper variant="outlined">` instead of hand-rolling surfaces with `<Box>`. Page Containers import `pageContainerSx` from `theme.ts`.

## Testing

Tests live in `Frigorino.Test/` and use xUnit + FakeItEasy. Database-touching tests use `Microsoft.EntityFrameworkCore.InMemory` via `TestApplicationDbContext`. There is no frontend test runner configured despite `knowledge/Frontend_Architecture.md` mentioning Jest — that section is aspirational, not current.
