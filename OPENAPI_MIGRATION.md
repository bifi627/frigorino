# Migration: Swashbuckle → Microsoft.AspNetCore.OpenApi

Status: planned, not started.
Owner: Dennis.
Estimated effort: ~half a day end-to-end (most of it is regenerating the TS client and chasing operationId-driven name churn).

## Context

The motivating problem is the `npm run api` workflow. Today it requires:

1. Start the backend with `--launch-profile Backend` (must be `Development` so user-secrets load → so the Postgres connection works → so `MigrateAsync` doesn't throw at boot).
2. Manually copy `/swagger/v1/swagger.json` into `ClientApp/src/lib/swagger.json`.
3. Run `npm run api` to regenerate the TS client.

That's three terminals, one boot-time DB hit, and three manual steps for what should be one command. The brittleness ate ~30 minutes during the recent slice migration.

Wider context, established by research (May 2026):

- The project is on `net10.0` but pinned to `Swashbuckle.AspNetCore 6.6.2` (current is `10.1.7` — well behind).
- Microsoft removed Swashbuckle from the `dotnet new webapi` template in .NET 9 ([dotnet/aspnetcore#54599](https://github.com/dotnet/aspnetcore/issues/54599)). Swashbuckle is **not deprecated**, but it's no longer the blessed path.
- The blessed path is `Microsoft.AspNetCore.OpenApi` (runtime) + `Microsoft.Extensions.ApiDescription.Server` (build-time MSBuild target that emits `openapi.json` to disk on `dotnet build`). No Kestrel, no DB, no secrets required.
- The community-blessed in-browser UI replacement is **Scalar** (`Scalar.AspNetCore`).

After migration, the workflow collapses to a single `npm run api` from `ClientApp/` — no backend boot, no manual copy.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Document generator | `Microsoft.AspNetCore.OpenApi` | First-party, ships in-box, MS-recommended for .NET 9+. |
| Build-time emitter | `Microsoft.Extensions.ApiDescription.Server` | MSBuild target — no separate dotnet tool install, runs on every `dotnet build`. |
| Dev-time UI | `Scalar.AspNetCore` | Community + MS-friendly default replacement for SwaggerUI. Modern UX. MIT-licensed. |
| Boot-time guard | `Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"` | The exact pattern from MS docs. Coexists with the existing `IntegrationTest` environment guard. |
| OpenAPI version | Stay on **3.0** for now | Current `swagger.json` is 3.0.1; openapi-typescript-codegen has known issues with 3.1 nullability. Bump to 3.1 as a separate follow-up. |
| Spec file naming | Rename `swagger.json` → `openapi.json` | Cosmetic but accurate — it's no longer a Swagger doc. Cheap to do during the same change. |
| Existing slice metadata | Keep as-is | Slices already use minimal-API metadata (`Produces<T>`, `WithTags`, `WithName`) which is what `Microsoft.AspNetCore.OpenApi` consumes natively. No code change inside slices. |
| Swashbuckle-specific code | None to migrate | Research confirmed zero `[SwaggerOperation]`, `SchemaFilter`, `OperationFilter`, or `IncludeXmlComments` usages. Nothing to port. |

## Package changes

`Application/Frigorino.Web/Frigorino.Web.csproj`:

- **Remove**: `<PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />`
- **Add**: `<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.*" />`
- **Add**: `<PackageReference Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.*" PrivateAssets="all" />` (build-only)
- **Add**: `<PackageReference Include="Scalar.AspNetCore" Version="*" />` (latest stable at implementation time)

Add a `<PropertyGroup>` to control where the generated spec lands and what it's called:

```xml
<PropertyGroup>
  <OpenApiDocumentsDirectory>ClientApp/src/lib</OpenApiDocumentsDirectory>
  <OpenApiGenerateDocumentsOptions>--file-name openapi</OpenApiGenerateDocumentsOptions>
</PropertyGroup>
```

This causes `dotnet build` on `Frigorino.Web.csproj` to emit `ClientApp/src/lib/openapi.json` after a successful build. The MSBuild target is up-to-date-aware: it only rewrites the file if the generated content actually changed.

> Per MS docs: do **not** also keep `Swashbuckle.AspNetCore.SwaggerGen` installed — its presence silently disables the build-time generator. Removing the entire `Swashbuckle.AspNetCore` package handles this.

## Code changes

### `Application/Frigorino.Web/Program.cs`

Three concrete edits.

**(a) Service registration — replace Swashbuckle with `AddOpenApi`.**

Current (Program.cs:15-18):
```csharp
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
```

New:
```csharp
builder.Services.AddControllers();
builder.Services.AddOpenApi();
```

(`AddEndpointsApiExplorer` is no longer needed — `AddOpenApi` registers the API description providers itself.)

**(b) Boot-time guards — extend the existing `IntegrationTest` pattern with a build-time-document check.**

Current (Program.cs:20-26):
```csharp
builder.Services.AddEntityFramework(builder.Configuration);
builder.Services.AddApplicationServices();
if (!builder.Environment.IsEnvironment("IntegrationTest"))
{
    builder.Services.AddFirebaseAuth(builder.Configuration);
}
builder.Services.AddMaintenanceServices();
```

New (top of file, near the `using`s):
```csharp
var isBuildTimeOpenApi =
    Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
```

Then update the Firebase guard:
```csharp
if (!builder.Environment.IsEnvironment("IntegrationTest") && !isBuildTimeOpenApi)
{
    builder.Services.AddFirebaseAuth(builder.Configuration);
}
```

And guard the database migrate (Program.cs:48-52):
```csharp
if (!isBuildTimeOpenApi)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}
```

The hazard analysis confirmed these are the only two boot-time crash sites without secrets/DB. `AddEntityFramework`, `AddApplicationServices`, `AddMaintenanceServices`, `AddSession`, `AddSpaStaticFiles`, etc. are all safe at registration time (verified at file-level — `EntityFramework/DependencyInjection.cs:14` defensively coalesces an empty connection string).

**(c) Pipeline — replace Swagger UI with Scalar.**

Current (Program.cs:56-60):
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

New:
```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();              // serves /openapi/v1.json
    app.MapScalarApiReference();   // serves /scalar/v1
}
```

### `Application/Frigorino.Web/ClientApp/vite.config.ts`

The dev proxy at line 65 forwards `/swagger/*` to the backend. Replace with the new paths so dev-mode SPA can hit the spec and the UI:

```ts
"^/openapi/*": { target, secure: false },
"^/scalar/*": { target, secure: false },
```

(Drop the `^/swagger/*` entry.)

### `Application/Frigorino.Web/ClientApp/package.json`

Current `api` script (line 11):
```json
"api": "npx openapi-typescript-codegen -i ./src/lib/swagger.json -o ./src/lib/api --client fetch --useUnionTypes --name FrigorinoApiClient"
```

New:
```json
"api:fetch": "dotnet build .. -c Debug",
"api:gen":   "openapi-typescript-codegen -i ./src/lib/openapi.json -o ./src/lib/api --client fetch --useUnionTypes --name FrigorinoApiClient",
"api":       "npm run api:fetch && npm run api:gen"
```

Notes:
- `dotnet build ..` from `ClientApp/` builds `Frigorino.Web.csproj` (the parent directory contains the project file). The MSBuild target then writes `openapi.json` into `ClientApp/src/lib/` thanks to the `OpenApiDocumentsDirectory` property.
- The two-step split (`api:fetch` / `api:gen`) lets you regen just the TS client off an existing `openapi.json` if you want to iterate on codegen flags without a full backend rebuild.

### `Application/Frigorino.Web/ClientApp/src/lib/`

- **Rename**: `swagger.json` → `openapi.json` (`git mv`). The build target will overwrite it on next build anyway, but renaming preserves git history.

### `CLAUDE.md`

Update two sections to reflect the new flow:

- The "Frontend" command list: replace the `npm run api` description with one that mentions the build-time generation (no separate backend start required).
- The "API surface" architecture note: replace the manual swagger.json copy workflow with "run `npm run api` from `ClientApp/`".

## Risks & expected churn

1. **Operation ID divergence → TS method-name churn.** Swashbuckle and `Microsoft.AspNetCore.OpenApi` use slightly different default operationId conventions:
   - Swashbuckle for controllers: `{Action}` (e.g. `getApiHousehold`, `postApiCurrentHousehold`).
   - `Microsoft.AspNetCore.OpenApi`: derives from `endpoint.Metadata.GetMetadata<EndpointNameMetadata>()` first, falling back to `{Action}` for controllers. Slices already set `WithName(...)`, so their names stay stable. Controllers without `[EndpointName]` may shift.
   - **Expected impact**: a one-time cascade of hook-call-site renames in `useHouseholdQueries.ts` and friends, identical in shape to the slice-migration cascades. Plan to set aside ~30 min for find/replace + `npm run lint`.
   - **Mitigation if churn is large**: add `[EndpointName("...")]` on the controller actions to lock names.

2. **Tag drift.** Current `swagger.json` has inconsistent tag names (e.g. `Household` vs `Households`, `Inventories` listed twice). This wasn't caused by Swashbuckle — it's an inconsistency in our `[ApiController]` / `WithTags(...)` choices. The migration **won't fix this** and shouldn't try to. Audit + cleanup is a separate follow-up.

3. **Dev-time UX change.** The familiar `/swagger` URL goes away. Replaced by `/scalar/v1` (UI) and `/openapi/v1.json` (raw spec). Dev bookmarks need updating once.

4. **Build now writes a tracked file.** Every successful `dotnet build` of `Frigorino.Web` (with no API changes) is a no-op for git because the MSBuild target only rewrites when content changed. But a build *after* an API change will modify `openapi.json` — desirable, but means `git status` after a backend change will show that file as modified until committed.

5. **Same package family, different versions.** `Microsoft.AspNetCore.OpenApi` and `Microsoft.Extensions.ApiDescription.Server` are versioned with the framework — pin to `10.0.*` to float patches without surprise majors (consistent with the project's existing dependency-pinning preference).

6. **Don't keep Swashbuckle around as a fallback.** Per MS docs, if `Swashbuckle.AspNetCore.SwaggerGen` is referenced (transitively too) it silently disables the build-time generator. Verify after the package swap that no transitive Swashbuckle reference survives (`dotnet list package --include-transitive`).

## Verification (in order)

1. `dotnet build Application/Frigorino.sln` — clean. Confirm `Application/Frigorino.Web/ClientApp/src/lib/openapi.json` exists and contains valid OpenAPI 3.0 JSON. Crucially: this build must succeed **without** `dotnet user-secrets` set or DB available — try it in a temporary terminal that has `ConnectionStrings__Database` unset to prove the boot-time guard works.
2. Diff the new `openapi.json` against the previous `swagger.json` (rename to `openapi.json` first). Expected differences: operationIds may shift on controller endpoints; everything else (paths, schemas, status codes) should match. Investigate any unexpected drift before proceeding.
3. From `ClientApp/`: `npm run api`. Confirm `src/lib/api/services/*.ts` regenerates and `src/lib/api/models/*.ts` is unchanged in structure.
4. `npm run lint` — surface any broken hook call-sites caused by operationId shifts. Fix them.
5. `npm run build` — clean (only the pre-existing chunk-size warning).
6. `dotnet test Application/Frigorino.sln` — all integration tests pass (they don't touch swagger but they exercise the whole boot path with the `IntegrationTest` env, validating that the new guard pattern coexists cleanly).
7. Manual smoke: `dotnet run --project Application/Frigorino.Web --launch-profile Backend` + `npm run dev` from `ClientApp/`. Visit `https://localhost:44375/scalar/v1` — the Scalar UI should render the API. Visit `https://localhost:44375/openapi/v1.json` — should serve the raw doc through the Vite proxy. Log in, switch household, confirm normal app function.
8. Run the existing Reqnroll integration test `SwitchActiveHousehold` — should pass (it doesn't depend on swagger but proves the runtime end-to-end works).

## Follow-ups (explicitly out of scope)

- Audit + clean up tag-name inconsistencies (`Household` vs `Households`, dup `Inventories`). One-shot cleanup, separate planning.
- Bump OpenAPI to 3.1 once `openapi-typescript-codegen` 3.1 support is verified (or migrate to a different codegen if it isn't).
- Update memory entry `feedback_api_regen_workflow.md` to reflect the new one-command flow (no longer "start backend with `--launch-profile Backend`, fetch /swagger, then npm run api").
- XML doc comments on slices/controllers → operation summaries in OpenAPI (purely cosmetic; nice for Scalar UX but not load-bearing).

## File reference

| File | Change |
|---|---|
| `Application/Frigorino.Web/Frigorino.Web.csproj` | Remove Swashbuckle, add 3 packages, add `OpenApi*` MSBuild props |
| `Application/Frigorino.Web/Program.cs` | Replace SwaggerGen/UseSwagger/UseSwaggerUI; add entry-assembly guard; extend Firebase guard; guard `MigrateAsync` |
| `Application/Frigorino.Web/ClientApp/vite.config.ts:65` | Drop `^/swagger/*`, add `^/openapi/*` and `^/scalar/*` |
| `Application/Frigorino.Web/ClientApp/package.json:11` | Replace `api` script; add `api:fetch` / `api:gen` |
| `Application/Frigorino.Web/ClientApp/src/lib/swagger.json` | `git mv` to `openapi.json` |
| `Application/Frigorino.Web/ClientApp/src/lib/api/**` | Will regenerate on `npm run api`; commit the diff |
| `Application/Frigorino.Web/ClientApp/src/hooks/useHouseholdQueries.ts` (and any other hooks) | Update call-sites for any operationIds that shifted |
| `CLAUDE.md` | Update Frontend commands + API surface sections |

## References

- [Generate OpenAPI documents — Microsoft Learn (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0)
- [Customize runtime behavior during build-time generation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0#customize-runtime-behavior-during-build-time-document-generation) — the `GetDocument.Insider` guard pattern
- [Scalar.AspNetCore on GitHub](https://github.com/scalar/scalar)
- [Swashbuckle removal from default templates — dotnet/aspnetcore#54599](https://github.com/dotnet/aspnetcore/issues/54599)
