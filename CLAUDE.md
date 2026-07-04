# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common

1. Ask, don't assume. If something is unclear, ask before writing a single line. Never make silent assumptions about intent, architecture, or requirements. When running unattended, pick the most reasonable interpretation, proceed, and record the assumption rather than blocking.

2. Implement the simplest solution for simple problems, better solutions for harder problems. Do not over-engineer or add flexibility that isn't needed yet.

3. Don't touch unrelated code but please do surface bad code or design smells you discover with me so we can address them as a separate issue.

4. Flag uncertainty explicitly. If you're unsure about something, see point 1 above. If it makes sense to do so, conduct a small, localised and low-risk experiment and bring the hypothesis and results to me to discuss. Confidence without certainty causes more damage than admitting a gap.

5. I'm always open to ideas on better ways to do things. Please don't hesitate to suggest a better way, or one that has long lasting impact over a tactical change. (as a few examples)

6. When a change invalidates something asserted here or in `knowledge/`, update the doc in the same change. Keep this file to rules, commands, invariants, and pointers — don't add enumerations of facts a grep can re-derive; they only drift.

## Project overview

Frigorino is a multi-tenant household management app (lists, inventories, recipes) built as a single deployable .NET 10 web application that serves a React SPA from `wwwroot` in production. In development the SPA is served by Vite and proxies API/openapi/scalar calls to the backend.

## Repository layout

```
Application/
  Frigorino.sln
  Frigorino.Domain/          # Entities (factories + aggregate methods), value objects, service interfaces, FluentResults errors
  Frigorino.Features/        # Vertical slices: one file per endpoint, request/response DTOs colocated
  Frigorino.Infrastructure/  # EF Core (Postgres), auth, background work, AI, file storage, push — wired via Add* DI extensions
  Frigorino.Web/             # ASP.NET Core host, MapGroup slice wiring, middleware; legacy scaffold controllers
    ClientApp/               # React 19 + Vite + TanStack Router SPA — has its own CLAUDE.md with frontend conventions
  Frigorino.Test/            # xUnit + FakeItEasy; aggregate-method + slice unit tests; ArchUnitNET layer rules
  Frigorino.IntegrationTests/# Reqnroll (BDD) + Playwright + Postgres Testcontainers — drives the SPA end-to-end
  Dockerfile                 # Multi-stage: builds backend + ClientApp, copies build → wwwroot
knowledge/                   # Longer-form architecture notes — start at knowledge/README.md (the index)
```

The Clean Architecture dependency direction is enforced by project references AND ArchUnitNET tests in `Frigorino.Test/Architecture/ArchitectureTests.cs`: `Domain` depends on no infrastructure frameworks, and neither `Infrastructure` nor `Features` references `Web`. Infrastructure is wired into the host via `Add*` DI extension methods called from `Frigorino.Web/Program.cs` (grep there for the current set).

Deeper notes live in `knowledge/` — `knowledge/README.md` is the index (pattern / capability / feature docs; a feature doc is the self-contained, citable unit for a spec/plan). Read the matching doc before bigger changes.

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
npm ci                 # clean install (npm install only when adding/changing a dependency)
npm run dev            # Vite dev server on https://localhost:44375 (proxies /api, /openapi, /scalar to :5001)
npm run build          # tsc -b && vite build → outputs to ClientApp/build (copied to wwwroot in Docker)
npm run lint           # eslint .
npm run tsc            # type-check only (tsc -b)
npm run prettier       # prettier --write .   (CI guard: npm run prettier:check)
npm run fix            # eslint . --fix && prettier --write .
npm run api            # rebuild backend (emits ./src/lib/openapi.json via MSBuild target) + regenerate ./src/lib/api
npm run routes:check   # regenerate routeTree.gen.ts + fail on diff (CI guard against a stale committed tree)
```

Frontend verification = `lint` + `tsc` + `prettier:check`.

Docker (full stack image, used in deployment):

```powershell
docker build -f Application/Dockerfile -t frigorino .
```

The Dockerfile publishes `Frigorino.Web` (solution-wide restore, web-only publish) and builds the SPA in parallel stages, then copies the SPA `build/` output into `wwwroot/` of the final image.

## Configuration

`Frigorino.Web/appsettings.json` has empty placeholders for all secrets — they MUST be supplied via user-secrets, environment variables, or `appsettings.Development.json`:

- `ConnectionStrings:Database` — Postgres connection string OR a `postgres://` URL (auto-converted by a static helper in `Infrastructure/EntityFramework/DependencyInjection.cs`).
- `FirebaseSettings:ValidIssuer` / `ValidAudience` / `AccessJson` — Firebase JWT validation + service account JSON.
- `Ai:ApiKey` + `Ai:Classifier:*` / `Ai:QuantityExtractor:*` / `Ai:RecipeTagSuggester:*` — OpenAI key + per-feature model + `Enabled` flags; each AI feature no-ops (`Null*` impl) unless the key **and** its flag are set (`knowledge/AI_Classification.md`). The per-feature model choices are intentional — don't normalize them.
- `FileStorage:Provider` (`Local`/`Gcs`) + `Bucket` / `Environment` / `LocalPath` — blob storage for recipe attachments + list-item media (`knowledge/File_Storage.md`).
- `MaintenanceSettings:TriggerToken` — shared secret guarding the `/internal/expiry-scan` cron endpoint (`knowledge/Push_Notifications.md`).
- `OpenTelemetry:*` — OTLP export endpoint/headers/protocol (`knowledge/Observability.md`).
- SPA env vars are `VITE_*`, read via `import.meta.env` — each must be declared as **both** `ARG` and `ENV` in the Dockerfile `build_frontend` stage (and set in every Railway env) or it silently no-ops in the bundle.

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

- Write-via-factory template: `Application/Frigorino.Features/Households/CreateHousehold.cs` — the rules-as-comments header at the top of the file is the authoritative slice contract and **overrides `Vertical_Slices.md` when they drift**.
- Write-via-aggregate-method template: `Application/Frigorino.Features/Households/Members/AddMember.cs` (most complex — cross-aggregate user resolution + 3 internal branches).
- Domain marker errors: `Application/Frigorino.Domain/Errors/DomainErrors.cs`.
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`.

Everything under `Frigorino.Features` is a slice; the only controllers left (`Frigorino.Web/Controllers/`) are non-domain scaffold (`Auth`, `Demo`, `WeatherForecast`). Per-feature decisions and dropped-endpoint rationale live in the `knowledge/` feature docs. **When adding a new endpoint, write a slice; do not add controllers.**

### Request pipeline (`Frigorino.Web/Program.cs`)

Order matters: `UseSession` runs before `UseAuthentication`/`UseAuthorization`. Lazy `Users`-row sync runs inside `JwtBearerEvents.OnTokenValidated` (`Frigorino.Infrastructure/Auth/FirebaseAuth.cs` → `UserSync.EnsureAsync`) — gated on the JWT's `auth_time` claim so it fires once per real Firebase login, not per request. `MapControllers` is followed by `UseSpa` + `MapFallbackToFile("index.html")` so unknown routes fall through to the React app.

### Multi-tenant household context

- `ICurrentUserService` resolves the user identity (id/email/name) from the Firebase JWT claims — it deliberately injects no DbContext. The lazy `User`-row creation on first login happens in `UserSync` (see Request pipeline above), not here.
- `ICurrentHouseholdService` keeps the active household ID in the **HTTP session** (`AddSession`, 30-min idle) and persists it to `User.LastActiveHouseholdId` as a durable fallback. Switching households mutates session state, not the JWT — this is why session middleware is mandatory.
- All household-scoped slices should go through these interfaces rather than reading claims directly.
- `UserHousehold` is the join entity carrying a `Role` (`HouseholdRole`: Owner/Admin/Member) — permission checks live in `Household` aggregate methods.
- `IsActive` soft-delete and automatic `CreatedAt`/`UpdatedAt` are managed centrally in `ApplicationDbContext` (timestamps auto-stamped in `SaveChangesAsync`; `IsActive` is filtered per-slice, not via a global query filter). New entities should follow the same pattern instead of setting timestamps in handlers.

### Background work

Full detail: `knowledge/Backend_Architecture.md` ("Background work"). The rules:

- **No wall-clock schedulers and no Hangfire** — Railway's serverless tier sleeps the container on idle, so schedulers silently miss their window and Hangfire's always-on Postgres polling defeats the sleep (tried and reverted).
- Periodic work = an `IMaintenanceTask` run once per cold start by `MaintenanceHostedService` (register in `AddMaintenanceServices`). Every new soft-delete aggregate needs its own purge clause in `DeleteInactiveItems` — no test enforces this.
- Request-triggered fire-and-forget = `BackgroundTaskQueue` (in-memory `System.Threading.Channels`, best-effort, lost on restart). Durable/cron work — the expiry scan — runs synchronously in-request behind the key-guarded `/internal/expiry-scan` endpoint instead (`knowledge/Push_Notifications.md`).

### API surface

- All endpoints are slices registered in `Program.cs` via `app.MapGroup(prefix).RequireAuthorization().WithTags(...)` groups + per-slice `Map*` extension methods; `MapControllers` remains only for the scaffold. In Development the spec is served at `/openapi/v1.json` and the [Scalar](https://scalar.com) UI at `/scalar/v1`.
- Wire contract: enums serialize as their **string names** (a `JsonStringEnumConverter` registered on both the slice and controller JSON options in `Program.cs`); the DB stores enums as **int** (EF default). The `IntegerSchemaTransformer` (`Frigorino.Web/OpenApi/`) collapses CLR int primitives only — it never touches string enum schemas.
- Client regeneration is one command from `ClientApp/`: `npm run api` (build-time MSBuild target emits `src/lib/openapi.json`, [`@hey-api/openapi-ts`](https://heyapi.dev) regenerates `src/lib/api/`, which is committed). No backend boot, no DB. Full pipeline: `knowledge/API_Integration.md`.
- The build-time OpenAPI generator runs the app entry point under `GetDocument.Insider`; code paths that need real config (Firebase auth, EF migrations) are gated behind the `isBuildTimeOpenApi` check in `Program.cs`.

### Frontend

Frontend conventions (routing, TanStack Query hook shapes, styling, i18n, push/PWA) live in `Application/Frigorino.Web/ClientApp/CLAUDE.md` — auto-loaded when working under `ClientApp/`.

## Testing

Tests live in `Frigorino.Test/` (xUnit + FakeItEasy) — unit tests for aggregate methods and slice logic. **Any test that exercises real database behavior uses Postgres Testcontainers in `Frigorino.IntegrationTests/` (Reqnroll + Playwright, drives the SPA end-to-end) — do not add SQLite or EF InMemory database tests; they diverge from real Postgres (collations, `ExecuteDeleteAsync`, relational query semantics).** (Some legacy `Frigorino.Test` slice tests still use EF InMemory via `TestApplicationDbContext`; don't extend that pattern for new DB-dependent coverage.) There is no frontend (JS) test runner configured.
