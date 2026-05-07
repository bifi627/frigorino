# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Frigorino is a multi-tenant household management app (lists + inventories) built as a single deployable .NET 8 web application that serves a React SPA from `wwwroot` in production. In development the SPA is served by Vite and proxies API/openapi/scalar/hangfire calls to the backend.

## Repository layout

```
Application/
  Frigorino.sln
  Frigorino.Domain/          # Entities, DTOs, service interfaces (no dependencies)
  Frigorino.Application/     # Service implementations, DTO mapping extensions
  Frigorino.Infrastructure/  # EF Core (Postgres), Firebase auth, Hangfire jobs, OpenAI client
  Frigorino.Web/             # ASP.NET Core host, controllers, middleware, Hangfire wiring
    ClientApp/               # React 19 + Vite + TanStack Router SPA
  Frigorino.Test/            # xUnit + FakeItEasy + EF InMemory
  Dockerfile                 # Multi-stage: builds backend + ClientApp, copies build → wwwroot
knowledge/                   # Longer-form architecture notes (read these before bigger changes)
```

The Clean Architecture dependency direction is enforced by project references: `Web → Application → Domain` and `Web → Infrastructure → Domain`. `Application` does not reference `Infrastructure` — Infrastructure types are wired in via DI extension methods (`AddEntityFramework`, `AddApplicationServices`, `AddFirebaseAuth`, `AddHangfireServices`) called from `Frigorino.Web/Program.cs`.

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
- `FirebaseSettings:ValidIssuer` / `ValidAudience` / `AccessJson` — Firebase JWT validation + service account JSON.
- `HangfireAuth:Username` / `Password` — basic-auth gate for the `/hangfire` dashboard. **Startup throws if password is empty**, so the app will not boot without these set.
- `OpenAiSettings:APIKey` / `Model` — used by `ClassificationService` for article classification.

## Architecture notes

### Request pipeline (`Frigorino.Web/Program.cs`)
Order matters: `UseSession` runs before `UseAuthentication`/`UseAuthorization`, then `InitialConnectionMiddleware` runs after auth (it reads the authenticated user). `MapControllers` is followed by `UseSpa` + `MapFallbackToFile("index.html")` so unknown routes fall through to the React app.

### Multi-tenant household context
- `ICurrentUserService` resolves the user from the Firebase JWT and lazily creates a `User` row on first login.
- `ICurrentHouseholdService` keeps the active household ID in the **HTTP session** (`AddSession`, 30-min idle). Switching households mutates session state, not the JWT — this is why session middleware is mandatory.
- All household-scoped controllers/services should go through these interfaces rather than reading claims directly.
- `UserHousehold` is the join entity carrying a `Role` (Owner/Admin/Member) — permission checks live in the service layer.
- `IsActive` soft-delete and automatic `CreatedAt`/`UpdatedAt` are managed centrally in `ApplicationDbContext`. New entities should follow the same pattern instead of setting timestamps in services.

### Background jobs (Hangfire)
The previous in-process `IMaintenanceTask` / `MaintenanceHostedService` system has been **replaced by Hangfire** backed by Postgres (`schema=hangfire`). `MaintenanceService.cs` is now empty and should not be used. Knowledge docs in `knowledge/Backend_Architecture.md` predate this change — trust the code.

Wiring lives in `Frigorino.Web/Services/HangfireDependencyInjection.cs`:
- Jobs are registered as scoped services and discovered by Hangfire's DI activator.
- `ConfigureHangfireJobs()` (called at the end of `Program.cs`) declares recurring jobs via `RecurringJob.AddOrUpdate<T>(...)`. Adding a new recurring job means: implement `Frigorino.Infrastructure.Jobs.<JobName>` with an `ExecuteAsync()` method, register it in `AddHangfireServices`, and add an `AddOrUpdate` entry here.
- `Cron.Never` is used to register manually-triggered jobs (e.g. `ClassifyListsJob`) so they appear in the dashboard but don't run on a schedule.
- The dashboard at `/hangfire` is gated by `HangfireAuthorizationFilter` (basic auth from `HangfireAuth:*` config).

### API surface
- Controllers in `Frigorino.Web/Controllers/` expose REST endpoints. In Development, the spec is served at `/openapi/v1.json` and the [Scalar](https://scalar.com) UI at `/scalar/v1` (replaces the old SwaggerUI).
- `JsonStringEnumConverter` is registered globally — enums serialize as strings on the wire, and the frontend's generated client expects string enums.
- OpenAPI is generated via `Microsoft.AspNetCore.OpenApi` + `Microsoft.Extensions.ApiDescription.Server`. `dotnet build Frigorino.Web` writes `ClientApp/src/lib/openapi.json` (configured via `OpenApiDocumentsDirectory` + `OpenApiGenerateDocumentsOptions` in the csproj — `--openapi-version OpenApi3_0` keeps the spec on 3.0 to match `openapi-typescript-codegen` expectations).
- The build-time generator runs the app entry point with a mock server. Code paths that require real config (Firebase auth, EF migrations) are gated behind `var isBuildTimeOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"` in `Program.cs`.
- The frontend client is generated by `npm run api` from `src/lib/openapi.json`. The full workflow is just one command from `ClientApp/`: change controllers/DTOs → `npm run api` (rebuilds the backend, emits the spec, regenerates the TS client). No backend boot, no DB, no manual copy. The generated code under `src/lib/api/` is committed.

### Frontend
- TanStack Router uses **file-based routing**; `routeTree.gen.ts` is auto-generated by the `@tanstack/router-plugin/vite` plugin — do not edit by hand.
- `_protected.tsx` is the auth-gated layout; routes inside it require a Firebase user.
- Server state goes through TanStack Query hooks in `src/hooks/use*Queries.ts`; client state uses Zustand. Do not introduce a third state layer (Redux, Context-as-store) for new features.
- All API calls go through `ClientApi` (singleton in `src/common/apiClient.ts`) which injects Firebase ID tokens via the `TOKEN` async resolver.
- i18n is wired via `i18next` + `i18next-http-backend` — translation files live under `src/i18n/`.

## Testing

Tests live in `Frigorino.Test/` and use xUnit + FakeItEasy. Database-touching tests use `Microsoft.EntityFrameworkCore.InMemory` via `TestApplicationDbContext`. There is no frontend test runner configured despite `knowledge/Frontend_Architecture.md` mentioning Jest — that section is aspirational, not current.
