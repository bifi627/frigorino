# Observability

Frigorino emits telemetry to a single **Grafana Cloud** stack from both the .NET backend (OpenTelemetry → Tempo / Mimir / Loki) and the React frontend (Grafana Faro). One vendor, one stack, three environments separated by a `deployment.environment` resource attribute (`local` / `stage` / `prod`).

This doc describes the *current* shape of the stack. For the rollout history, open questions, and parked alternatives, see `../OBSERVABILITY.md`.

## Components

| Layer | Tool | What lands |
|---|---|---|
| Backend traces | OpenTelemetry → Grafana Tempo | ASP.NET Core request spans, EF Core query spans, outgoing HTTP client spans |
| Backend metrics | OpenTelemetry → Grafana Mimir | ASP.NET Core request metrics, EF Core counters, .NET runtime (GC, threads, heap, lock contention) |
| Backend logs | OpenTelemetry → Grafana Loki | `ILogger` records with semantic attributes; structured |
| Frontend RUM | Grafana Faro | Web Vitals (LCP/FID/CLS/TTFB), page views, browser errors, console logs, session/view tracking |
| Frontend traces | `@grafana/faro-web-tracing` → Tempo | `fetch` / `XHR` spans correlated with backend spans via W3C `traceparent` |

Everything is single-stack: `https://otlp-gateway-prod-eu-west-2.grafana.net/otlp` (backend OTLP) and a Faro collector URL with the app key embedded as the path segment.

## Backend wiring

### Packages (`Frigorino.Web`)

The `OpenTelemetry.*` package group — `Extensions.Hosting`, `Exporter.OpenTelemetryProtocol`, and the `AspNetCore` / `EntityFrameworkCore` / `Http` / `Runtime` instrumentations — is exact-pinned in `Frigorino.Web.csproj`. Read current versions there rather than duplicating them here.

### Registration site

`Frigorino.Web/Program.cs` registers OTel via `services.AddOpenTelemetry().WithMetrics(...).WithTracing(...)` + `builder.Logging.AddOpenTelemetry(...)`, sitting after the build-time-OpenAPI guard so the spec generator stays untouched.

### Per-signal exporter configuration

Each of the three signals (traces / metrics / logs) registers its own `AddOtlpExporter(opt => ConfigureOtlpFor(signalPath, opt))`. Two non-obvious bits:

- **Endpoint path is appended manually** per exporter: `/v1/traces`, `/v1/metrics`, `/v1/logs`. Setting `OtlpExporterOptions.Endpoint` programmatically silently sets the internal `AppendSignalPathToEndpoint = false` — the SDK only auto-appends when the endpoint comes from the `OTEL_EXPORTER_OTLP_ENDPOINT` env var. Symptom of getting this wrong: 404 from the OTLP gateway, no further info.
- **Single transport carries all three signals.** Grafana Cloud's gateway routes per path automatically.

### Resource attributes

Set via `ConfigureResource(...)`:
- `service.name = "frigorino-web"`
- `service.version` from the assembly version
- `deployment.environment` mapped from `ASPNETCORE_ENVIRONMENT`: `Production` → `prod`, `Staging` → `stage`, anything else → `local`

Every dashboard query must filter on `deployment.environment` so local dev data never bleeds into stage/prod views.

### Tracing filter

`AddAspNetCoreInstrumentation(opt => opt.Filter = ...)` drops spans for `/openapi/*`, `/scalar/*`, `/healthz`, `/readyz`. The corresponding metric filter is deferred — route cardinality on `http.server.request.duration` is bounded enough not to threaten the active-series ceiling yet.

### Diagnostics

To debug why nothing reaches Grafana, drop an `OTEL_DIAGNOSTICS.json` next to the csproj (`Frigorino.Web/`): it switches on the OTel SDK's own self-diagnostics, written to `Frigorino.Web.exe.<pid>.log` (gitignored), capturing exporter failures and instrument warnings. No such file is committed — none is present by default; add it for an investigation and delete it after. The log file caps at the configured size then truncates.

## Frontend wiring

### Packages (`ClientApp`)

`@grafana/faro-web-sdk` + `@grafana/faro-web-tracing`, versioned in `ClientApp/package.json` (caret-minor, per the project's npm pinning convention).

### Adapter

`ClientApp/src/common/observability.ts` exposes four helpers:
- `initObservability()` — called once at the top of `src/main.tsx`, before the auth/i18n side-effect imports, so the SDK's `fetch`/XHR hooks are installed before any request fires.
- `identifyUser({id, email})` / `resetUser()` — called from `ClientApp/src/common/authProvider.ts` on Firebase auth state changes (and from the Playwright test-user bypass branch).
- `pushPageView(path)` — called from `router.subscribe("onResolved", ...)` in `src/main.tsx`, giving us a canonical `pageview` event alongside Faro's automatic History-API view instrumentation.

All four helpers are **no-ops when `VITE_FARO_URL` is empty** (the local-opt-in gate).

### What's automatic vs manual

Automatic via `getWebInstrumentations()`:
- Web Vitals (LCP / FID / CLS / TTFB)
- Uncaught browser errors (`window.onerror` + `unhandledrejection`)
- Console warns/infos/errors
- Session + view tracking

Manual (per the adapter):
- User identification (`identifyUser`/`resetUser`) on auth changes
- Pageview events with explicit `path`
- (Future) business events — see [IDEAS.md "Frontend business events"](../IDEAS.md)

### Trace correlation

`new TracingInstrumentation()` in the instrumentation list attaches a W3C `traceparent` header to every `fetch` / XHR. Because the SPA and API are served from the same Railway host, propagation works **same-origin without `propagateTraceHeaderCorsUrls`** — no allow-list config needed. A frontend interaction → backend span chain shows up as one trace in Tempo. If the API ever moves to a separate subdomain, add the regex list to `TracingInstrumentation`.

### CORS allowlist (Faro app config)

The "credential" baked into `VITE_FARO_URL` is public-by-design. Abuse defense is the **CORS allowlist on the Faro app itself**, set in Grafana Cloud → Frontend Observability → Apps → Edit. Allowed origins should be `https://localhost:44375` (Vite dev), the stage URL, and the prod URL. If a 204 preflight succeeds but the actual POST fails with "CORS error", that's the allowlist needing an entry.

## Environment-variable contract

### Backend (`Frigorino.Web`)

| Key | Required | Notes |
|---|---|---|
| `OpenTelemetry:OtlpEndpoint` | yes | `https://otlp-gateway-prod-eu-west-2.grafana.net/otlp`. Committed in `appsettings.json`. Non-secret. |
| `OpenTelemetry:OtlpHeaders` | **gate** | `Authorization=Basic <base64>` for Grafana Cloud OTLP ingest. **Empty → OTel never registers.** Stored in user-secrets locally, Railway env in stage/prod. Real secret. |
| `OpenTelemetry:OtlpProtocol` | optional | Defaults to `http/protobuf` in `appsettings.json`. |
| `ASPNETCORE_ENVIRONMENT` | environmental | Maps to `deployment.environment` (`Production`/`Staging`/other → `prod`/`stage`/`local`). |

**Scale-to-zero tradeoff — leave `OtlpHeaders` UNSET on free-tier Railway.** The metrics exporter is a `PeriodicExportingMetricReader` (60s) and runtime instrumentation always has a value, so when OTel is registered the backend POSTs a telemetry batch to Grafana every 60s **even with zero users**. That steady outbound egress resets Railway's idle timer and the container never sleeps. On any Railway environment that should scale-to-zero, do **not** set `OpenTelemetry__OtlpHeaders` (the gate stays closed → no exporter → no heartbeat → container sleeps; a real request wakes it via inbound traffic). This disables *all* backend telemetry in that env — accepted for free-tier UAT. Frontend Faro is unaffected: it runs in the browser, not the container, so it generates no container egress and `VITE_FARO_URL` can stay set. See [memory: no-synthetic-uptime-checks](../C--Repositories-frigorino/memory/project_no_synthetic_uptime_checks.md).

### Frontend (`ClientApp`)

| Key | Required | Notes |
|---|---|---|
| `VITE_FARO_URL` | **gate** | Full Grafana Cloud Faro collector URL with the app-key path segment. **Empty → SDK never initializes.** Public-by-design. |
| `VITE_FARO_APP_NAME` | optional | Defaults to `frigorino-web`. Must match the Faro app name configured in Grafana. |
| `VITE_FARO_ENV` | optional | `prod` / `stage` / `local`. Defaults to `local`. Railway sets it per environment. |
| `VITE_APP_VERSION` | optional | Defaults to `0.0.0`. Wire to git SHA at deploy time if useful. |

**Build-time vs runtime — Railway gotcha.** Vite reads `VITE_*` from `process.env` and inlines values into the JS bundle **at build time**. Railway service variables are only visible to a Dockerfile build if the Dockerfile declares them with `ARG` (Railway forwards them as `--build-arg`). `Application/Dockerfile`'s `build_frontend` stage declares `ARG VITE_FARO_URL` / `VITE_FARO_APP_NAME` / `VITE_FARO_ENV` / `VITE_APP_VERSION` and promotes them to `ENV` before `npm run build`. Symptom of removing or forgetting an `ARG`: SPA logs `[Faro] Not initialized (VITE_FARO_URL is empty)` in prod even though the variable is set in Railway.

### Local opt-in

Both layers default to **off** locally. Setting `OpenTelemetry:OtlpHeaders` (via `dotnet user-secrets`) activates backend OTel; setting `VITE_FARO_URL` (via `ClientApp/.env.local`, gitignored) activates Faro. Either layer can be on or off independently. Local data is tagged `environment=local` so dashboards filtering `=~"stage|prod"` won't pick it up.

## Trace stitching mechanics

1. The user clicks something. TanStack Query calls `fetch("/api/...")`.
2. Faro's `TracingInstrumentation` wraps the `fetch`, generates a span, attaches `traceparent: 00-<traceId>-<spanId>-01`.
3. ASP.NET Core's OTel instrumentation reads `traceparent` on the inbound request, creates a child span under the same `traceId`.
4. EF Core spans for any DB queries nest under the ASP.NET span.
5. All spans flow to Tempo with the same `traceId`. Tempo's UI (and Faro's "view trace" link) renders the chain end-to-end.

The frontend span shows the network time visible to the browser; the backend spans show server-side processing. Together they reveal whether a slow request is server-side compute, DB time, or network latency.

## What is deliberately NOT instrumented

- **Synthetic uptime checks** (Grafana Synthetics, Healthchecks.io, periodic `/healthz` pings). Railway free tier sleeps the container on idle; periodic external probes defeat that and push compute past free-tier limits. Downtime is detected passively: a spike of Faro `fetch` errors means users are seeing it, Railway's own status surfaces hard outages. Revisit on a paid Railway plan or a hosting move. See [memory: no-synthetic-uptime-checks](../C--Repositories-frigorino/memory/project_no_synthetic_uptime_checks.md).
- **Grafana IRM alerting.** Most of the alerts we'd configure (`/healthz` down) presuppose synthetics. Without those, on-call paging is more noise than signal at one-UAT-client scale. Faro errors + manual checks suffice today.
- **Pyroscope (continuous profiling).** No perf question is currently unanswerable by traces. Adopt when one appears (likely candidate: a slow background maintenance task or DB query).
- **k6 Cloud (synthetic load testing).** Same reasoning as Synthetics — would warm the container. The local `k6` CLI remains available for ad-hoc load runs against stage if needed.
- **PostHog or any second analytics vendor.** Faro covers the use cases at current scale. Revisit triggers (product team needing self-serve funnels, pixel-perfect replay, first-class feature flags, surveys) are listed in `../OBSERVABILITY.md`.
- **Source-map upload** (`@grafana/faro-rollup-plugin`). Deferred until the first frontend crash makes minified stack traces an actual problem; the plumbing is well-understood and one PR away.

## Where to look in Grafana

- **Backend latency / error rate** → Mimir, query `http.server.request.duration` filtered by `deployment.environment` and route.
- **A specific user's session** → Faro → Sessions, search by `user.id` (Firebase UID).
- **Frontend Web Vitals** → Frontend Observability → Web Vitals tab. Pre-built.
- **A slow request end-to-end** → Faro → Errors/Slow Requests → click "View Trace" → Tempo shows the full frontend + backend chain.
- **A specific backend log line** → Loki, query `{service.name="frigorino-web", deployment.environment="prod"} |= "search string"`.
- **Maintenance task activity** → Loki, filter the `MaintenanceHostedService` / `DeleteInactiveItems` log lines emitted on each cold start.
