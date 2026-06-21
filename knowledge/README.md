# knowledge/

Longer-form architecture notes for Frigorino. Read the relevant doc before a bigger change. The docs split three ways:

- **Pattern docs** — *how we build*. Cross-cutting conventions referenced by every feature. One copy, never duplicated per feature.
- **Capability docs** — *shared services* many features lean on (AI, storage, auth).
- **Feature docs** — *what we built*. One self-contained reference per user-facing area. **A feature doc is the citable unit for a spec or plan**: it covers the feature's domain → API surface → key flows → decisions → cross-feature touchpoints → frontend, and links out to the pattern/capability docs it relies on rather than restating them. `Recipes.md` is the template exemplar.

> Each feature doc follows the same section set: Overview · Domain · API surface · Key flows · Key decisions & rationale · Cross-feature touchpoints · Frontend · Config (if any) · Links out.

## Pattern docs — how we build

- [Vertical_Slices.md](Vertical_Slices.md) — slice anatomy (one file = one endpoint), where domain rules live, `Result`→HTTP mapping. **Cite when adding or changing any endpoint.**
- [Backend_Architecture.md](Backend_Architecture.md) — request pipeline order, multi-tenant household context, session, background/maintenance jobs, DI wiring.
- [Frontend_Architecture.md](Frontend_Architecture.md) — TanStack Router file-routing, Query + Zustand state, per-route auth gating, the configured fetch client.
- [Frontend_Styling.md](Frontend_Styling.md) — what the theme owns; MUI conventions; what not to inline. **Cite for any UI styling.**
- [API_Integration.md](API_Integration.md) — OpenAPI generation + hey-api client + the one-hook-per-file query/mutation conventions and `npm run api` workflow.
- [Testing.md](Testing.md) — xUnit/FakeItEasy units vs Reqnroll/Playwright/Postgres-Testcontainers integration tests (no SQLite/EF-InMemory).
- [Observability.md](Observability.md) — OpenTelemetry (backend) + Grafana Faro (frontend).
- [Performance_Optimization.md](Performance_Optimization.md) — performance practices.

## Capability docs — shared services

- [AI_Classification.md](AI_Classification.md) — OpenAI product classification + quantity extraction pipeline, gating/flags, the product catalog. **Cite for AI features, the product catalog, item-text routing.**
- [File_Storage.md](File_Storage.md) — blob storage (Local/GCS), image processing, orphan reclamation, blob areas. **Cite for attachments / media.**
- [Firebase_Auth_Setup.md](Firebase_Auth_Setup.md) — Firebase JWT validation, service account, dev-auth bypass.

## Feature docs — one reference per user-facing area

- [Households.md](Households.md) — the tenant container: household CRUD, settings, sort blueprints, active-household + user settings. **Canonical home for the small-aggregates / not-a-god-aggregate rationale.**
- [Members.md](Members.md) — membership + roles (Owner/Admin/Member), as `Household` aggregate methods.
- [Lists.md](Lists.md) — shopping lists; items (text + media), checking, fractional-index ordering, promote-to-inventory, blueprint apply.
- [Inventories.md](Inventories.md) — stock locations with expiry; expiry calendar; per-user notification opt-out.
- [Recipes.md](Recipes.md) — recipes (items/sections/links/attachments), copy-to-list. *Template exemplar.*
- [Push_Notifications.md](Push_Notifications.md) — expiry-digest push via FCM, the synchronous cron scan, push-only service worker.

Per-feature decisions are folded into each feature doc above; there is no separate migration-history archive.
