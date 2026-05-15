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

---

## Post-deploy smoke tests (health endpoints + CI smoke)

- **Why:** Nothing on Railway currently verifies a deploy is healthy before traffic switches. A misconfigured Firebase audience, rotated OpenAI key, missing Hangfire credentials, or a partial EF migration only surfaces when a real user hits the app. CI tests run against Testcontainers and a fake auth handler, so they miss every secret/config/binding issue that's unique to the deployed environment — and they can't see stale build artifacts (`openapi.json`, `routeTree.gen.ts`, the SPA bundle in `wwwroot`) baked into the actual deployed image. Smoke tests at deploy time fill that gap with a thin-and-broad "the building isn't on fire" check. Especially valuable given stage→main FF promotion: an early smoke failure on stage prevents promoting broken bits to the real client environment.
- **Sketch:**
  - **Layer 1 — ASP.NET HealthChecks (backend, deploy-gating):**
    - Add `AspNetCore.HealthChecks.NpgSql` (pin exact version per dependency rule). Built-in `Microsoft.Extensions.Diagnostics.HealthChecks` covers the rest.
    - Register two endpoints in `Frigorino.Web/Program.cs`:
      - `/healthz` — liveness only, **no dependencies**. Returns 200 if the process answers. Used as the liveness probe. Critically: do NOT put the DB check here, or a transient DB blip restarts the container needlessly.
      - `/readyz` — readiness. Runs:
        1. Postgres connectivity via `AddNpgSql` (trivial `SELECT 1`). Same DB also covers Hangfire storage since it uses the `hangfire` schema on the same Postgres — no separate Hangfire check needed.
        2. Custom `ConfigHealthCheck` that asserts `FirebaseSettings.AccessJson` / `ValidIssuer` / `ValidAudience` and `OpenAiSettings.APIKey` are non-empty. **Config-loaded check, not an external ping** — cheap, offline-safe, catches the most common rotation/typo failure mode without burning OpenAI quota or hammering Firebase on every Railway poll.
    - Both endpoints anonymous (gate around `UseAuthentication`/`UseAuthorization` in pipeline order). Excluded from `InitialConnectionMiddleware` so they don't trigger the user-lazy-create path.
    - Add `/api/version` slice returning the build SHA (injected via Docker build arg or `<SourceRevisionId>` MSBuild property surfaced to runtime via assembly metadata). Anonymous. Two purposes: enables Layer 2's "is the right image actually deployed" assertion, and helps human debugging.
    - Point Railway's `healthcheckPath` at `/readyz` (in `railway.toml` or service settings). **Verify before implementing** that Railway's healthcheck actually gates the traffic switch (vs. just marking unhealthy after the fact) — doesn't change the design but determines whether Layer 1 alone is deploy-gating.
  - **Layer 2 — Post-deploy GitHub Actions smoke (CI, catches what readiness can't):**
    - New workflow `.github/workflows/smoke.yml` (or step appended to existing deploy workflow). Triggered after Railway reports deploy success — either via Railway webhook, polling `/api/version` until it matches the expected SHA, or running after a delay matched to typical Railway deploy time.
    - Steps:
      1. `GET /healthz` → expect 200.
      2. `GET /readyz` → expect 200; on failure, print response body so the failing subcheck is visible in CI logs.
      3. `GET /openapi/v1.json` → parse with `jq`, assert `.paths | length > N` where N is the current path count minus slack. Catches a Docker build that skipped or cached out the OpenAPI generation MSBuild target, leaving a stale spec served to clients.
      4. `GET /` → expect `Content-Type: text/html`, assert body contains the SPA root marker (`id="root"` or app title). Catches `MapFallbackToFile` misordering that would return `index.html` for `/api/*` requests or vice versa.
      5. `GET /api/version` → assert returned SHA matches the commit SHA from the GitHub Actions context. Catches stale-image deploys — a real risk with FF promotion where the image could come from anywhere.
    - Run on both stage and main deploys. Failure on stage = block promotion to main; failure on main = page / rollback signal.
  - **Out of scope for initial introduction:**
    - Continuous synthetic monitoring (Healthchecks.io / BetterUptime polling `/healthz` every minute) — catches slow-burn issues (cert expiry, quota exhaustion, DB pool drift) unrelated to deploy. Add later if/when it's actually needed.
    - Playwright-driven "real user" smoke against a synthetic Firebase test account. This is the only layer that would catch SPA bundling/routing regressions inside the auth-gated routes, but maintenance cost is non-trivial (managing a synthetic Firebase user) and it can't deploy-gate (too slow). Defer until deploy frequency or a real bundling incident justifies it.
- **Impact / cost:**
  - Layer 1: ~3 files (`ConfigHealthCheck.cs`, version slice, `Program.cs` registration), one NuGet package, one Railway config tweak. Deploy-gating once Railway `healthcheckPath` points at `/readyz`. Highest leverage per LOC of any smoke option.
  - Layer 2: ~1 GitHub Actions workflow file, zero production code (assuming `/api/version` already shipped from Layer 1). Catches build-artifact regressions and stale-image deploys that readiness can't see.
  - The two layers compose: Layer 1 prevents broken traffic-switches; Layer 2 catches the things that survive a healthy readiness check (stale `openapi.json`, wrong image SHA, broken SPA fallback).

---

## Cold-start UX: wake-ping + static-file optimizations

- **Why:** Railway runs with sleep mode enabled for cost-saving, so a cold visitor today blocks 5-15s on a blank page while the container wakes, runs `MigrateAsync` (`Program.cs:56-62`), initializes Firebase auth, and starts Kestrel. For a stage environment that hosts a real client UAT, this is the worst possible first impression. The cold-start is *sequential*: nothing renders until origin responds. A handful of cheap changes can parallelize the wakeup with user interaction and make the cold path materially faster in absolute terms — without changing deployment shape. Highest leverage per hour of work; ships in pieces; no migration risk. Investigated as an alternative to splitting the SPA off to a CDN — both lower-cost and lower-risk than the full split for the current scale.
- **Sketch:**
  - **Wake-ping on SPA load (biggest perceived win):**
    - SPA fires a fire-and-forget `GET /healthz` the moment `main.tsx` mounts, before Firebase auth completes. This wakes the Railway container in parallel with the user reading the login UI / entering credentials.
    - Requires `/healthz` to be anonymous, dependency-free, and excluded from `InitialConnectionMiddleware` (otherwise it triggers the lazy-create-user path on an unauthenticated request). This endpoint overlaps exactly with what the "Post-deploy smoke tests" idea plans as Layer 1 — these two ideas should ship together, or smoke-tests first.
    - Frontend implementation: one `fetch` call near app entry, no error handling, no UI binding. ~5 LOC.
    - **Open question:** also fire from the TanStack Router prefetch hook so route transitions warm subsequent dependencies? Probably overkill — once the singleton is up for the session, the rest is free.
  - **Pre-compressed static assets at build time (not runtime):**
    - `UseResponseCompression` middleware adds CPU cost during cold-start exactly when we can least afford it. Better: emit `.br` and `.gz` siblings at `vite build` time via `vite-plugin-compression` (or rollup-native equivalent), then have ASP.NET serve the pre-compressed file based on `Accept-Encoding`.
    - ASP.NET static-files middleware doesn't pick up pre-compressed siblings automatically; needs either a custom `IFileProvider` wrapper or a small piece of middleware ahead of `UseStaticFiles` that rewrites the request path to `.br` / `.gz` when the client supports it. ~30 LOC, well-trodden pattern.
    - Cost paid once at build, served from disk forever after. Compression CPU never touches the hot path.
  - **Tighten static-file cache headers:**
    - Vite-hashed assets (`/assets/*-[hash].js`, etc.) → `Cache-Control: public, max-age=31536000, immutable`. ASP.NET default doesn't set this — needs `UseStaticFiles(new StaticFileOptions { OnPrepareResponse = ... })` with a branch on the hashed-filename pattern.
    - `index.html` → `Cache-Control: no-cache, must-revalidate`. Always revalidate the entry point; everything it references is immutable, so once `index.html` is fresh, the rest is free from any cache.
    - These headers also become the contract that the Cloudflare reverse-proxy idea relies on — both ideas should be designed together even if shipped in sequence.
  - **ReadyToRun (R2R) at publish:**
    - Add `<PublishReadyToRun>true</PublishReadyToRun>` to `Frigorino.Web.csproj` (or pass as an MSBuild property in the Dockerfile `dotnet publish` step). Pre-compiles IL to native at publish time, so the JIT doesn't pay first-tier compilation on cold start.
    - Typical cold-start improvement: ~20-40%. Trade-off: ~15-25% larger published output (more disk in the image).
    - **Do not** use full AOT (`PublishAot=true`) — EF Core, MVC model binding, runtime OpenAPI generation, and reflection-based JSON serialization all break under AOT without significant restructuring. R2R is the conservative, safe win.
  - **Considered and dropped:**
    - Postgres connection-pool eager warmup: Railway colocates DB and app; first connection latency is small. Not worth the startup hook.
    - Skipping `MigrateAsync()` when no migrations are pending: the pending-check itself opens a DB connection, so net win is near zero.
    - Deferring Hangfire init: scheduler thread is already started lazily; not worth restructuring.
- **Impact / cost:** ships in independent pieces. ~5 LOC frontend wake-ping (overlaps with smoke-tests idea — coordinate the endpoint). ~30 LOC backend for cache headers + pre-compressed serving. One Vite plugin dependency, one csproj property for R2R, one Dockerfile tweak if pre-compression runs in the frontend build stage. No breaking changes, no migration, no API surface change. Best case: cold-start UX goes from "blank page for 10s" to "app shell loads from device while backend warms in background." Total effort: ~half-day across all four items if done in sequence.

---

## Edge-cached reverse proxy in front of Railway

- **Why:** With Railway sleep mode enabled, even after the wake-ping/R2R wins in "Cold-start UX: wake-ping + static-file optimizations", the *very first* request to a sleeping origin still pays cold-start in full because the wake-ping IS that first request. Putting an edge-caching reverse proxy in front of Railway lets `index.html` and Vite-hashed assets serve from edge cache while the origin is asleep — the user sees the SPA load instantly, Firebase auth runs entirely client-side (`apiClient.ts:13`), and the SPA's wake-ping kicks off the backend wakeup in parallel with their login interaction. Captures most of the UX benefit of a full frontend-on-CDN split (investigated and decided against for the current scale) without the costs: no CORS, no session-cookie migration, no two-deploy coordination, no integration-test rewiring. Bonus on most providers: free WAF / DDoS protection, edge HTTP/3, reduced egress from Railway.
- **Provider choice (not pinned):** the capability that matters is *reverse proxy to an arbitrary HTTP origin + rule-based path caching + programmatic purge API*. Several providers do this:
  - **Cloudflare (free plan)** — recommended default. Free tier covers everything. Dashboard UX is the most beginner-friendly. Generous bandwidth.
  - **BunnyCDN** — cheap pay-as-you-go (~$0.005/GB), supports custom origins, simple purge API. Often faster than Cloudflare in EU/APAC.
  - **AWS CloudFront** — most configurable, integrates with the rest of AWS if relevant; more setup, paid per-request + bandwidth.
  - **Fastly / KeyCDN** — similar shape, fewer reasons to pick over the above unless already in use.
  - **Firebase Hosting is NOT a fit for this pattern.** Its `rewrites` only proxy to Cloud Run, Cloud Functions, or another Firebase Hosting site — *not* to arbitrary external origins like Railway. Firebase Hosting would be a natural pick if revisiting the full SPA-on-static-host split (since the project already uses Firebase for identity), but for keeping the monolithic Railway deploy with edge caching in front, it's the wrong tool. Don't confuse the two architectures.
  - Decide before implementing. Concrete sketch below assumes Cloudflare since it's the recommended default, but the same shape applies to any of the alternatives.
- **Sketch:**
  - **DNS + proxy setup:**
    - Add the production domain to the chosen provider. For Cloudflare: either full nameserver transfer (recommended; full feature access) or partial CNAME setup if the domain is locked elsewhere.
    - Enable proxying on the A/CNAME record pointing to Railway.
    - SSL mode: **Full (strict)** — provider terminates TLS at edge with its own cert, re-encrypts to origin using Railway's cert. Never use Flexible mode equivalents — they leak plaintext on the last hop.
  - **Caching rules:**
    - Two cacheable scopes:
      - `/` and `/index.html` → **Cache Everything**, edge TTL ~1 hour, **ignore cookies** in cache key (otherwise auth/session cookies fragment the cache per-user, killing hit rate and risking cross-user response bleed).
      - `/assets/*` → respect origin `Cache-Control` headers (`immutable, max-age=31536000` after the cold-start-ux idea ships). The provider will cache effectively forever automatically.
    - Explicit **bypass** for: `/api/*`, `/openapi/*`, `/scalar/*`, `/hangfire/*`. Either via a higher-priority bypass rule or by naming the cacheable paths positively and leaving the rest at default.
    - For the `/` rule, **ignore query strings** in cache key — Vite-hashed assets have none, and `?utm_source=...` shouldn't fragment.
  - **Deploy-time cache purge:**
    - After Railway reports deploy healthy (or after the smoke-test workflow passes, per "Post-deploy smoke tests"), call the provider's purge API for `/` and `/index.html`. Single `curl` step in the deploy workflow, ~10 lines of YAML.
    - Hashed assets don't need purging — their filenames change each build, so the new `index.html` references new asset names that miss the cache → fetched from origin → cached → served. Old asset names age out naturally and never become stale-yet-served, because nothing references them.
  - **Stale-window failure mode (the only real risk):**
    - Between Railway "deploy healthy" and purge completion, edge POPs may still serve old `index.html`. Old `index.html` references old hashed asset filenames, but the old image is gone from origin → those requests 404 → broken page. Three mitigations to evaluate:
      1. **Just accept it** (preferred). Window is seconds; global purge typically completes in ~30s. PWA service worker (`VitePWA` with `autoUpdate`) already serves the previous SPA from device cache for returning users, masking the window for them entirely. Only new visitors hitting in the exact window see the broken state, and a refresh recovers them.
      2. Short edge TTL with `stale-while-revalidate` semantics — accepts more origin requests as the cost of self-healing without manual purge.
      3. Keep N old SPA builds in `wwwroot` for grace coverage. Complicates the Dockerfile, requires multi-stage retention; not worth it for a stage environment.
  - **Caveats to verify before flipping (varies by provider):**
    - WebSocket support: not needed today, but check support if any future feature uses WS.
    - Hangfire `/hangfire` dashboard: works through proxy as long as the bypass-cache rule covers it. Basic-auth header passes through fine on every provider listed.
    - HTTP request timeout (e.g. Cloudflare free is 100s, others vary): no current endpoint exceeds anything reasonable. Track if a long-poll/SSE endpoint is added.
  - **Stage + prod:** same proxy pattern with separate subdomains/origins. Cache rules scope per hostname on every provider.
- **Impact / cost:** zero application code changes if the cold-start-ux idea has shipped first (this idea's contract is the cache headers it produces). Setup is mostly dashboard work plus one purge step in the deploy workflow. ~4-8 hours total including learning the provider's rule UI, testing the stale-window behavior, and wiring the deploy purge. Cloudflare free plan covers everything at zero cost; other providers cents/GB. Reversible — if it goes wrong, flip the proxy off and DNS reverts to direct-to-Railway within minutes.

---

## Observability stack: PostHog + Grafana Cloud (two-tool consolidation)

- **Why:** Today there is no production error visibility beyond Railway's log tail, no product analytics, no APM, no Hangfire silent-failure detection, and no alerting on anything other than service-down. For a stage environment hosting a real client UAT, this is the gap that turns "client emails about something broken" into "we have no idea what happened." Two distinct problem spaces are involved: **monitoring** (errors, backend metrics, traces, logs, uptime, cron heartbeats) and **product analytics** (which features the client actually uses). Investigated a three-tool split (Sentry + Grafana + PostHog) and ruled it out in favor of two tools after PostHog's 2024 error-tracking launch made Sentry redundant at this scale — PostHog free covers 100k exceptions/month vs Sentry's 5k, with unlimited team members vs Sentry's 1-user cap. Verified all free tiers comfortably fit current scale by 100-1000×, but the two-tool shape lowers cognitive overhead and consolidates everything user-facing (errors + replay + analytics + flags) into one place.
- **Why not one tool:** neither product absorbs the other's job. PostHog has zero backend infrastructure observability — no metrics, no APM, no log aggregation, no synthetic monitoring. Grafana can do crude product analytics via LogQL queries on structured log events, but funnels, retention curves, and cohort analysis become DIY queries instead of click-through dashboards. Two tools is the floor; below that, something material is given up.
- **Sketch:**
  - **PostHog (user-facing observability):**
    - Cloud free tier. Frontend SDK `posthog-js` in `main.tsx`. Server-side `PostHog.NET` (or current canonical package — verify name at adoption time) in `Program.cs` for events originating from Hangfire jobs and background work.
    - **Identity link**: when Firebase auth state changes (`authProvider.ts:29-31`), call `posthog.identify(firebaseUser.uid, { email, name })`. Stitches anonymous pre-login events to the authenticated user.
    - **Event taxonomy — explicit, not autocaptured.** Initialize with `posthog.init({ autocapture: false })`. Define ~10-15 events mapping to real features:
      - Household lifecycle: `household_created`, `household_switched`, `household_deleted`
      - Membership: `member_invited`, `member_role_changed`, `member_removed`
      - Lists / inventories (once migrated to slices): `list_item_added`, `inventory_item_classified`, etc.
    - **Error tracking:** `posthog.captureException` on the frontend, .NET SDK exception capture on the backend. Wire source maps via the PostHog Vite plugin during `vite build` — without source maps, frontend stack traces are minified gibberish and the feature is useless.
    - **Session replay:** enable with **all input fields masked by default**, plus CSS-selector masking for household names, list-item names, and inventory-item names — household contents are user-private and shouldn't appear in replays even for the UAT client. Sample rate 100% initially (5k recordings/month is plenty at current scale); throttle later if needed.
    - **Quota disciplines (the only PostHog free-tier footguns):**
      - Autocapture off (as above) — otherwise 1M events/month vanishes in days.
      - PII masking on replay (as above).
      - Don't put `posthog.isFeatureEnabled()` inside tight loops — each call consumes flag-request quota.
  - **Grafana Cloud (infra observability):**
    - **OpenTelemetry in `Program.cs`:** add `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.EntityFrameworkCore`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` (pinned exact versions per the dependency-pinning rule). Configure `AddOpenTelemetry().WithMetrics(...).WithTracing(...).WithLogging(...)` exporting via OTLP to Grafana Cloud's ingest endpoint. Credentials supplied via environment variables, matching the Firebase / Hangfire secret pattern (placeholders in `appsettings.json`).
    - **Cardinality discipline (the 10k active series ceiling is easy to blow past):**
      - **Do not** include `household_id` or `user_id` as metric labels — tenant cardinality kills the budget. Keep tenant identifiers on traces and logs (GB-budgeted, not series-budgeted), never on metrics.
      - Drop `/openapi/*`, `/scalar/*`, `/hangfire/*`, `/healthz` from HTTP server metrics — they're noise. Implement via an OTel view/filter processor in `Program.cs`.
    - **Synthetic monitoring** (replaces the Healthchecks.io recommendation that floated in earlier discussions):
      - One API check pinging `/healthz` every few minutes — both wakes the Railway container on schedule (interacts with "Cold-start UX: wake-ping + static-file optimizations") and detects downtime.
      - One synthetic heartbeat per Hangfire `RecurringJob`: the job ends by hitting a Grafana Cloud Synthetics push URL. If the heartbeat is missed by N minutes, Grafana alerts. Catches the silent-stop failure mode the Hangfire dashboard cannot detect (see `HangfireDependencyInjection.ConfigureHangfireJobs`). Pairs with "Post-deploy smoke tests"'s `/readyz` for deploy-time gating.
    - **Logs**: drain Railway logs to Loki via OTLP. Use structured logging (`ILogger` with semantic properties, not interpolated strings). 50GB/month is ~1.5GB/day — comfortably above what this app generates.
    - **Tracing**: ASP.NET + EF Core instrumentation auto-handles HTTP and DB spans. Hangfire jobs need explicit `ActivitySource` spans if you want them traced — worth doing for the slower jobs (`ClassifyListsJob`).
    - **Alerting + on-call**: Grafana IRM free tier covers 3 active users. Initial alerts: error-rate spike, Hangfire heartbeat miss, `/healthz` downtime, p95 latency above N seconds (calibrate after baseline).
    - **Faro (frontend RUM):** hold off. `posthog-js` already captures Web Vitals. Only add Faro if a need for deeper frontend tracing emerges — extra JS bundle + redundant collection otherwise.
  - **Cold-start interaction with both SDKs:**
    - Sleep mode means events buffered when the container terminates are lost unless explicitly flushed. PostHog .NET SDK and OpenTelemetry both expose flush APIs.
    - Register `IHostApplicationLifetime.ApplicationStopping` callbacks that call `posthog.Flush()` and `tracerProvider.ForceFlush()` before shutdown. Belt-and-braces: flush from the global exception handler too.
- **What's deliberately dropped vs the three-tool plan:**
  - **Sentry**: dropped. PostHog error tracking is younger (2024) than Sentry's (12+ years), so grouping heuristics and stack-trace symbolication are less polished — but at current error volume (low double digits/month) the gap doesn't bite, you'll read each error anyway. Sentry's 1-user free-tier cap made team scaling expensive. **Revisit Sentry if** error volume crosses a few hundred per day and PostHog's grouping starts producing noisy duplicates, or if release-comparison/regression-detection features more refined than PostHog's become important. Migration is a config change, not a rewrite — both SDKs sit at the same call sites.
  - **Healthchecks.io**: dropped. Grafana Synthetics covers cron heartbeats with the same shape and consolidates monitoring into one dashboard.
  - **Firebase Analytics (`getAnalytics` import in `auth.ts:20-21`)**: dead code today; delete as part of this work. PostHog replaces what it would have done.
- **Out of scope for the initial introduction (defer until concrete demand):**
  - PostHog **feature flags** — initialize the SDK so the API is available, but don't add flags until there's a real toggle use case. Each evaluation consumes quota.
  - PostHog **experiments / A/B testing**, **surveys**, **LLM analytics** — included in the free tier, no need to enable until a specific use case arises.
  - Grafana **Pyroscope** (continuous profiling) — interesting but needs a real perf problem to justify. Defer.
  - Grafana **Faro** (frontend RUM beyond Web Vitals) — covered by PostHog at this scale.
  - **Multi-environment separation** in PostHog/Grafana — important once stage and prod actually diverge in usage patterns. Initial pass tags events with `environment: stage|prod` and splits projects later.
- **Impact / cost:**
  - **Frontend**: ~30 LOC across `main.tsx` (init), `authProvider.ts` (identify on sign-in), and a Vite plugin entry for PostHog source-map upload. Two new npm dependencies (`posthog-js`, PostHog Vite plugin), pinned exact versions.
  - **Backend**: ~50 LOC in `Program.cs` for OTel registration + filtering processor, ~5 LOC per Hangfire job for heartbeat ping. Four to five new NuGet packages (OpenTelemetry suite + PostHog .NET), pinned exact versions.
  - **Config**: 4-6 new environment variables (Grafana Cloud OTLP endpoint + credentials, PostHog project key + host). `appsettings.json` placeholder pattern matching Firebase.
  - **Dead code removal**: drop `getAnalytics(app)` + the analytics import from `auth.ts:20-21` in the same change.
  - **Docs**: small updates to `CLAUDE.md` (observability section) and `knowledge/Backend_Architecture.md`. Both knowledge files have stale sections elsewhere (`Backend_Architecture.md` still mentions `MaintenanceHostedService` post-Hangfire migration) — clean as part of touching them.
  - **Time**: ~1 full day for initial wire-up of both tools, ~half-day for event-taxonomy + masking rules + alert-threshold calibration. Reversible — either tool can be swapped or disabled with config changes only.
  - **Ongoing cost**: $0 unless usage crosses free-tier ceilings (currently 100-1000× below all of them).
