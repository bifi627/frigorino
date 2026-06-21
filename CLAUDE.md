# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Frigorino is a multi-tenant household management app (lists, inventories, recipes) built as a single deployable .NET 10 web application that serves a React SPA from `wwwroot` in production. In development the SPA is served by Vite and proxies API/openapi/scalar calls to the backend.

## Repository layout

```
Application/
  Frigorino.sln
  Frigorino.Domain/          # Entities (factories + aggregate methods), value objects (Quantity, Product classification), service interfaces, FluentResults errors, IMaintenanceTask
  Frigorino.Features/        # Vertical slices: one file per endpoint, request/response DTOs colocated
  Frigorino.Infrastructure/  # EF Core (Postgres), Firebase + dev auth, background maintenance, OpenAI classification/extraction, GCS/local file storage + image processing, FCM push
  Frigorino.Web/             # ASP.NET Core host, MapGroup slice wiring, middleware; a few legacy controllers (Auth/Demo)
    ClientApp/               # React 19 + Vite + TanStack Router SPA
  Frigorino.Test/            # xUnit + FakeItEasy; aggregate-method + slice unit tests; ArchUnitNET layer rules
  Frigorino.IntegrationTests/# Reqnroll (BDD) + Playwright + Postgres Testcontainers — drives the SPA end-to-end
  Dockerfile                 # Multi-stage: builds backend + ClientApp, copies build → wwwroot
knowledge/                   # Longer-form architecture notes (read these before bigger changes)
```

The Clean Architecture dependency direction is enforced by project references AND ArchUnitNET tests in `Frigorino.Test/Architecture/ArchitectureTests.cs`: `Domain` depends on no infrastructure frameworks, `Infrastructure` does not reference `Web`, and `Features` does not reference `Web`. Infrastructure types are wired into the host via DI extension methods (`AddEntityFramework`, `AddFirebaseAuth`, `AddDevAuth`, `AddBackgroundTaskQueue`, `AddFileStorage`, `AddImageProcessing`, `AddItemClassification`, `AddQuantityExtraction`, `AddRecipeQuantityExtraction`, `AddMaintenanceServices`, `AddExpiryNotifications`) called from `Frigorino.Web/Program.cs`.

Deeper notes live in `knowledge/` — start at `knowledge/README.md` (the index). It splits into **pattern** docs (`Backend_Architecture`, `Vertical_Slices`, `Frontend_Architecture`, `Frontend_Styling`, `API_Integration`, `Testing`, `Observability`, `Performance_Optimization`), **capability** docs (`AI_Classification`, `File_Storage`, `Firebase_Auth_Setup`), and **feature** docs (`Households`, `Members`, `Lists`, `Inventories`, `Recipes`, `Push_Notifications`) — a feature doc is the self-contained, citable unit for a spec/plan.

## Common commands

Backend (run from repo root or `Application/`):
```powershell
dotnet restore Application/Frigorino.sln
dotnet build   Application/Frigorino.sln
dotnet run     --project Application/Frigorino.Web         # starts API on https://localhost:5001
dotnet test    Application/Frigorino.sln                   # runs Frigorino.Test + Frigorino.IntegrationTests
dotnet test    Application/Frigorino.Test --filter "FullyQualifiedName~<TestClass>"
dotnet ef migrations add <Name> --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Migrations are applied automatically at startup via `context.Database.MigrateAsync()` in `Program.cs`.

Frontend (run from `Application/Frigorino.Web/ClientApp/`):
```powershell
npm install
npm run dev            # Vite dev server on https://localhost:44375 (proxies /api, /openapi, /scalar to :5001)
npm run build          # tsc -b && vite build → outputs to ClientApp/build (copied to wwwroot in Docker)
npm run lint           # eslint .
npm run fix            # eslint . --fix && prettier --write .
npm run tsc            # type-check only (tsc -b)
npm run api            # rebuild backend (emits ./src/lib/openapi.json via MSBuild target) + regenerate ./src/lib/api
```

Docker (full stack image, used in deployment):
```powershell
docker build -f Application/Dockerfile -t frigorino .
```
The Dockerfile publishes `Frigorino.Web` (solution-wide restore, web-only publish) and builds the SPA in parallel stages, then copies the SPA `build/` output into `wwwroot/` of the final image.

## Configuration

`Frigorino.Web/appsettings.json` has empty placeholders for all secrets — they MUST be supplied via user-secrets, environment variables, or `appsettings.Development.json`:
- `ConnectionStrings:Database` — Postgres connection string OR a `postgres://` URL (auto-converted by a static helper in `Infrastructure/EntityFramework/DependencyInjection.cs`).
- `FirebaseSettings:ValidIssuer` / `ValidAudience` / `AccessJson` — Firebase JWT validation + service account JSON.
- `Ai:ApiKey` + `Ai:Classifier:*` / `Ai:QuantityExtractor:*` — OpenAI key + per-feature model + `Enabled` flags; both AI features no-op (Null* triggers) unless key **and** flag are set (`knowledge/AI_Classification.md`).
- `FileStorage:Provider` (`Local`/`Gcs`) + `Bucket` / `Environment` / `LocalPath` — blob storage for recipe attachments + list-item media (`knowledge/File_Storage.md`).
- `MaintenanceSettings:TriggerToken` — shared secret guarding the `/internal/expiry-scan` cron endpoint (`knowledge/Push_Notifications.md`).
- `OpenTelemetry:*` — OTLP export endpoint/headers/protocol (`knowledge/Observability.md`).

### Local dev: two modes

The dev-auth bypass is **opt-in, not a committed default**. Manual `dotnet run` / `npm run dev` keep the real Firebase flow (requires user-secrets).

- **Real Firebase + your DB** (default): `dotnet run --project Application/Frigorino.Web` + `npm run dev`. Uses your user-secrets.
- **Bypass + local Postgres** (agents / fresh-clone): `powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1` (down: same with `dev-down.ps1`). Activated by the `LocalDb` launch profile (sets `DevAuth__Enabled=true` + local conn string) and `$env:VITE_DEV_AUTH=true` set by the script before spawning vite. Identity on both sides: `dev-user` / `dev@frigorino.local`.

Bypass implementation: `DevAuthHandler` (`Frigorino.Infrastructure/Auth/DevAuthHandler.cs`, gated on `Development` env + `DevAuth:Enabled`) and `authProvider.ts` (gated on `VITE_DEV_AUTH`). `/readyz` returns 503 in bypass mode (cosmetic — `/healthz` still 200).

Agent skills wrap the scripts: `/dev-up` (auto-invokable, gated on actual UI-verification need) and `/dev-down` (manual-only) — see those `SKILL.md` files for the per-worktree port-scan (`.dev/stack.json`, scanned above the user's 5001/44375/5432/8080 so worktrees coexist), `.dev/*.log` logging, Windows-cascade teardown, and Playwright MCP `--isolated` pairing.

## Architecture notes

### Vertical slice architecture

Authoritative shape: `knowledge/Vertical_Slices.md`. Trust it over older architecture notes.

The whole API is vertical slices. Each slice = one file = one endpoint, with request DTO + response DTO + endpoint registration + handler colocated. Domain rules (validation, role policy, aggregate invariants) live in `Frigorino.Domain` — either in entity factories (`Entity.Create`) for construction or in aggregate methods (`aggregate.DoXxx`) for mutations. Domain methods return `FluentResults.Result<T>`; the slice handler dispatches by error type (`EntityNotFoundError` → 404, `AccessDeniedError` → 403, generic `Error` with `Property` metadata → `ValidationProblem`). Reads stay handler-only — inline EF projection into the response DTO (no mapping libraries).

Canonical references:
- Write-via-factory template: `Application/Frigorino.Features/Households/CreateHousehold.cs` (rules-as-comments header at lines 1-20 — overrides `Vertical_Slices.md` when they drift; the factory write template itself is at lines 32-65).
- Write-via-aggregate-method template: `Application/Frigorino.Features/Households/Members/AddMember.cs` (most complex — cross-aggregate user resolution + 3 internal branches).
- Domain marker errors: `Application/Frigorino.Domain/Errors/DomainErrors.cs`.
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`.

All features are slices: Households (+ Members/Blueprints/Settings), Lists (+ Items/Blueprints/Promote), Inventories (+ Items/Settings/per-user Notifications), Recipes (+ Items/Sections/Links/Attachments/CopyToList), Me/ActiveHousehold/Settings, Notifications (FCM token register + expiry-scan trigger), Version. The only remaining controllers (`Frigorino.Web/Controllers/`) are non-domain scaffold (`Auth`, `Demo`, `WeatherForecast`). Per-feature decisions and dropped-endpoint rationale are folded into the feature docs under `knowledge/` (`Households.md`, `Members.md`, `Lists.md`, `Inventories.md`, …). **When adding a new endpoint, write a slice; do not add controllers.**

### Request pipeline (`Frigorino.Web/Program.cs`)
Order matters: `UseSession` runs before `UseAuthentication`/`UseAuthorization`. Lazy `Users`-row sync runs inside `JwtBearerEvents.OnTokenValidated` (`Frigorino.Infrastructure/Auth/FirebaseAuth.cs` → `UserSync.EnsureAsync`) — gated on the JWT's `auth_time` claim so it fires once per real Firebase login, not per request. `MapControllers` is followed by `UseSpa` + `MapFallbackToFile("index.html")` so unknown routes fall through to the React app.

### Multi-tenant household context
- `ICurrentUserService` resolves the user identity (id/email/name) from the Firebase JWT claims — it deliberately injects no DbContext. The lazy `User`-row creation on first login happens in `UserSync` (see Request pipeline above), not here.
- `ICurrentHouseholdService` keeps the active household ID in the **HTTP session** (`AddSession`, 30-min idle) and persists it to `User.LastActiveHouseholdId` as a durable fallback. Switching households mutates session state, not the JWT — this is why session middleware is mandatory.
- All household-scoped slices should go through these interfaces rather than reading claims directly.
- `UserHousehold` is the join entity carrying a `Role` (`HouseholdRole`: Owner/Admin/Member) — permission checks live in `Household` aggregate methods.
- `IsActive` soft-delete and automatic `CreatedAt`/`UpdatedAt` are managed centrally in `ApplicationDbContext` (timestamps auto-stamped in `SaveChangesAsync`; `IsActive` is filtered per-slice, not via a global query filter). New entities should follow the same pattern instead of setting timestamps in handlers.

### Background jobs (startup maintenance)
Periodic maintenance runs as an in-process startup batch, **not** a wall-clock scheduler — Railway's serverless tier sleeps the container on idle, so any scheduled job would silently miss its window. `MaintenanceHostedService` (`Frigorino.Infrastructure/Services/`) waits a few seconds after boot, then runs every registered `IMaintenanceTask` once inside a DI scope (per-task errors are logged, never crash startup). Three tasks run today: `DeleteInactiveItems` (purges soft-deleted households/lists/inventories/items, plus checked-off list items past 30 days), `ReclaimOrphanBlobs` (mark-and-sweep of unreferenced blobs per `IBlobReferenceSource` area — see `knowledge/File_Storage.md`), and `BackfillProductClassification` (enqueues classification for un-classified products — only registered when AI is enabled). Each re-runs on every cold start — i.e. roughly whenever the app is used after sleeping — cheap and idempotent. Add a task by implementing `IMaintenanceTask` and registering it in `AddMaintenanceServices` (`MaintenanceDependencyInjection.cs`).

Request-triggered fire-and-forget work should use an in-process `System.Threading.Channels` queue drained by a `BackgroundService` (event-driven, no idle polling), **not** Hangfire — reverted because its always-on `BackgroundJobServer` polls Postgres continuously and defeats Railway's serverless sleep (see IDEAS.md). This queue now exists — `BackgroundTaskQueue` + `QueuedHostedService` (`Frigorino.Infrastructure/Services/`), drained by a single bounded-concurrency consumer; it carries the AI classification + quantity-extraction jobs (`knowledge/AI_Classification.md`). The expiry-notification scan is the exception: it runs synchronously in-request behind the key-guarded `/internal/expiry-scan` endpoint (external Railway cron), writing its `NotificationDispatch` ledger row claim-slot-first so concurrent/duplicate calls are idempotent — durability the in-memory queue can't promise (`knowledge/Push_Notifications.md`).

### API surface
- The API is wired entirely as vertical slices in `Frigorino.Features`, registered in `Program.cs` via `app.MapGroup(prefix).RequireAuthorization().WithTags(...)` groups whose endpoints are added with per-slice extension methods (`households.MapCreateHousehold()`, `lists.MapCreateList()`, …). `MapControllers` remains only for the `Auth`/`Demo` scaffold. New endpoints are slices — see "Vertical slice architecture" above. In Development, the spec is served at `/openapi/v1.json` and the [Scalar](https://scalar.com) UI at `/scalar/v1`.
- Enums serialize as their **string names** on the wire (e.g. `HouseholdRole` emits `enum: ["Member","Admin","Owner"]` / TS string union) via a `JsonStringEnumConverter` registered on both `ConfigureHttpJsonOptions` (slices) and `AddControllers().AddJsonOptions` (scaffold) in `Program.cs`. The DB still stores enums as **int** (EF default — no migration). The `IntegerSchemaTransformer` (`Frigorino.Web/OpenApi/IntegerSchemaTransformer.cs`, registered via `AddSchemaTransformer` in `AddOpenApi`) only collapses CLR int **primitives** (e.g. stringified-number inputs) to plain integers; it does not touch string enum schemas.
- OpenAPI is generated via `Microsoft.AspNetCore.OpenApi` + `Microsoft.Extensions.ApiDescription.Server`. `dotnet build Frigorino.Web` writes `ClientApp/src/lib/openapi.json` (configured via `OpenApiDocumentsDirectory` + `OpenApiGenerateDocumentsOptions` in the csproj).
- The build-time generator runs the app entry point with a mock server. Code paths that require real config (Firebase auth, EF migrations) are gated behind `var isBuildTimeOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"` in `Program.cs`.
- The frontend client is generated by `npm run api` from `src/lib/openapi.json` via [`@hey-api/openapi-ts`](https://heyapi.dev) (config: `ClientApp/openapi-ts.config.ts`). The full workflow is one command from `ClientApp/`: change endpoints/DTOs → `npm run api` (rebuilds the backend, emits the spec, regenerates the TS client). No backend boot, no DB, no manual copy. Generated code under `src/lib/api/` is committed.

### Frontend
- TanStack Router uses **file-based routing**; `routeTree.gen.ts` is auto-generated by the `@tanstack/router-plugin/vite` plugin — do not edit by hand.
- Auth gating is per-route: each protected route's `beforeLoad` calls `requireAuth` (`src/common/authGuard.ts`), which reads the Zustand auth store and redirects to `/auth/login` when there's no Firebase user. (There is no `_protected` layout route.)
- Server state goes through TanStack Query. Every endpoint has generated hooks-ready helpers in `src/lib/api/@tanstack/react-query.gen.ts` (from the hey-api `@tanstack/react-query` plugin). One-hook-per-file under `features/<area>/` spreads the generated options into `useQuery`/`useMutation` — never write `queryFn`/`mutationFn`/`queryKey` by hand. See the "API hook conventions" section below. Client state uses Zustand. Do not introduce a third state layer (Redux, Context-as-store) for new features.
- Route files under `src/routes/` are thin shells: `createFileRoute` + `requireAuth` + import the page component from `features/<area>/pages/`. See `routes/household/create.tsx` for the canonical shape.
- The fetch client is configured once at app boot in `src/common/apiClient.ts` (imported for its side effect from `main.tsx`). It injects the Firebase ID token via a request **interceptor** (`client.interceptors.request.use`), not the generated `auth` resolver (ASP.NET emits no security schemes, so `auth` never fires). There is no `ClientApi` singleton — generated SDK functions import the configured `client` internally.
- First-run onboarding: `routes/index.tsx` redirects a signed-in user with no households to `/onboarding` (a household-create form with a Skip option) unless they've skipped, persisted via `features/households/onboardingSkip.ts`.
- i18n is wired via `i18next` + `i18next-http-backend` — translation files live under `ClientApp/public/locales/{en,de}/translation.json`. **Tests never assert on translated text** — see styling guide.
- Push + PWA: a **push-only** service worker (`src/sw.ts`, no Workbox precaching) plus `src/common/pushNotifications.ts` (FCM enable/disable/register), surfaced in `features/settings`; PWA install wiring in `src/common/pwa.ts`. Frontend RUM/observability via Grafana Faro (`src/common/observability.ts`). See `knowledge/Push_Notifications.md`.
- SPA env vars (all `VITE_*`, read via `import.meta.env`): `VITE_DEV_AUTH`, `VITE_FCM_VAPID_KEY`, `VITE_FARO_URL` / `_APP_NAME` / `_ENV`, `VITE_APP_VERSION`, `VITE_APP_NAME`. Each must be declared as **both** `ARG` and `ENV` in the Dockerfile `build_frontend` stage (and set in every Railway env) or it silently no-ops in the bundle.

### API hook conventions

Every TanStack Query hook in `features/<area>/use*.ts` follows one exact shape (no exceptions). Mirror the canonical files rather than copying snippets here: `features/lists/useList.ts` (query hook) and `features/lists/useDeleteList.ts` (mutation hook).

- **Query hook** — takes the IDs needed to build the URL, spreads `getXOptions({ path: {...} })` into `useQuery`, guards `enabled` on each path id being `> 0`, sets a `staleTime`.
- **Mutation hook** — arg-less; the caller passes the full `{ path, body }` to `mutate`/`mutateAsync`. Spread `xMutation()`; invalidation reads `variables.path.*` in `onSuccess`/`onSettled` and builds keys via `getXQueryKey({ path: {...} })`.

**Rules:**
- Never write `queryFn`, `mutationFn`, or manual `queryKey` arrays. Spread `getXOptions` / `xMutation` / `getXQueryKey()`.
- Never reintroduce `*Keys.ts` files — auto-generated keys carry `tags` for both point and tag-based invalidation. Tag-predicate invalidation isn't used anywhere today, but the keys support it if you ever need broad cross-domain invalidation: `queryClient.invalidateQueries({ predicate: q => (q.queryKey[0] as { tags?: string[] })?.tags?.includes('Households') })`.
- Mutation hooks are arg-less; callers pass `{ path: {...}, body: ... }` to `mutate` / `mutateAsync`.
- Optimistic update hooks (toggle/reorder/create/update items) keep their `onMutate`/`onError`/`onSettled` callbacks — those are the substance the codegen doesn't replace. Read/write queryKeys via `getXQueryKey({ path: {...} })`, not literal arrays.
- The error from a mutation may be widened to `unknown` when the OpenAPI error response is `unknown` (e.g. 404 bodies). When rendering in JSX, type the local as `unknown` and use `error instanceof Error ? error.message : t("common.errorOccurred")`.
- Hey-api throws the parsed error response body on non-2xx (because the generated mutationFn passes `throwOnError: true`). `instanceof ApiError` is no longer a thing — to read field-level validation errors, narrow on the body shape: `(error as { errors?: { email?: string[] } } | null)?.errors?.email`.

### Frontend styling

Authoritative shape: `knowledge/Frontend_Styling.md`. Trust it over the inline sx patterns still present in the legacy Lists/Inventories routes.

Key rules: the theme at `ClientApp/src/theme.ts` owns `shape.borderRadius`, responsive typography, and button overrides — don't reintroduce `borderRadius: 2`, manual `boxShadow`, or `fontSize: { xs, sm }` inline. Use MUI size props (`size="small"`, `fontSize="small"`) and `<Card elevation={N}>` / `<Paper variant="outlined">` instead of hand-rolling surfaces with `<Box>`. Page Containers import `pageContainerSx` from `theme.ts`.

## Testing

Tests live in `Frigorino.Test/` (xUnit + FakeItEasy) — unit tests for aggregate methods and slice logic. **Any test that exercises real database behavior uses Postgres Testcontainers in `Frigorino.IntegrationTests/` (Reqnroll + Playwright, drives the SPA end-to-end) — do not add SQLite or EF InMemory database tests; they diverge from real Postgres (collations, `ExecuteDeleteAsync`, relational query semantics).** (Some legacy `Frigorino.Test` slice tests still use EF InMemory via `TestApplicationDbContext`; don't extend that pattern for new DB-dependent coverage.) There is no frontend (JS) test runner configured.
