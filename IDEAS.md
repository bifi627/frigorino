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

## Edge-cached reverse proxy in front of Railway

- **Why:** With Railway sleep mode enabled, even with wake-ping + R2R shipped, the *very first* request to a sleeping origin still pays cold-start in full because the wake-ping IS that first request. Putting an edge-caching reverse proxy in front of Railway lets `index.html` and Vite-hashed assets serve from edge cache while the origin is asleep — the user sees the SPA load instantly, Firebase auth runs entirely client-side (`apiClient.ts:13`), and the SPA's wake-ping kicks off the backend wakeup in parallel with their login interaction. Captures most of the UX benefit of a full frontend-on-CDN split (investigated and decided against for the current scale) without the costs: no CORS, no session-cookie migration, no two-deploy coordination, no integration-test rewiring. Bonus on most providers: free WAF / DDoS protection, edge HTTP/3, reduced egress from Railway.
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
      - `/assets/*` → respect origin `Cache-Control` headers — the origin already serves `immutable, max-age=31536000` on hashed assets. The provider will cache effectively forever automatically.
    - Explicit **bypass** for: `/api/*`, `/openapi/*`, `/scalar/*`, `/hangfire/*`. Either via a higher-priority bypass rule or by naming the cacheable paths positively and leaving the rest at default.
    - For the `/` rule, **ignore query strings** in cache key — Vite-hashed assets have none, and `?utm_source=...` shouldn't fragment.
  - **Deploy-time cache purge:**
    - After Railway reports deploy healthy, call the provider's purge API for `/` and `/index.html`. Single `curl` step in the deploy workflow, ~10 lines of YAML.
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
- **Impact / cost:** zero application code changes — the cache headers this idea relies on already ship from origin. Setup is mostly dashboard work plus one purge step in the deploy workflow. ~4-8 hours total including learning the provider's rule UI, testing the stale-window behavior, and wiring the deploy purge. Cloudflare free plan covers everything at zero cost; other providers cents/GB. Reversible — if it goes wrong, flip the proxy off and DNS reverts to direct-to-Railway within minutes.

---

## Frontend business events (Faro `pushEvent`)

- **Why:** Faro is wired and capturing pageviews + errors + Web Vitals automatically. What it doesn't yet give us is **which features people actually use** — when someone creates a household, switches active household, invites a member, etc. Without these, dashboards show traffic but not behaviour, and "did anyone use the new feature?" is unanswerable. This is the natural follow-up to the Faro rollout and the missing piece for a usable product-side view in Grafana's Frontend Observability dashboards.
- **Sketch:**
  - Add a `pushEvent(name, attrs)` helper next to the existing `identifyUser` / `pushPageView` exports in `ClientApp/src/common/observability.ts`. No-op when Faro is gated off (mirrors the existing helpers).
  - Wire into the migrated vertical-slice hook layer. Inject the call from `onSuccess` of the mutation hook, not from the React component — so the event fires on real success, not on optimistic update.
  - **Event taxonomy v1** (frozen list; cardinality bounded by feature count, attribute values bounded by IDs):
    - Household lifecycle: `household_created`, `household_switched`, `household_deleted`
    - Membership: `member_invited`, `member_role_changed`, `member_removed`
  - **Attribute shape:** keep tenant IDs (`householdId`, `userId`) on events but NEVER on metric labels — the high-cardinality-on-metrics rule from the OTel side still applies (see `knowledge/Observability.md`). Faro events are not metrics so IDs as attributes are fine.
  - **PII discipline:** household names, list-item names, and inventory-item names are user-private content. Never include them in event attributes. IDs only.
  - **Lists / inventories events** (`list_item_added`, `inventory_item_classified`, etc.) — defer until those features are migrated to the vertical-slice pattern. Instrumenting the legacy controller layer would just have to be re-wired during slice migration.
- **Out of scope:**
  - Backend-emitted events. Hangfire jobs and other backend paths don't need to push Faro events; backend behaviour is already covered by OTel traces.
  - Cross-tool bridging (Faro events → Loki). They live in the Faro app inside Grafana Cloud; no bridging needed at this scale.
  - Custom funnels / retention curves. Grafana's Frontend Observability standard cuts cover event volume by user / env. DIY LogQL funnel queries are possible but only worth building when a product team starts consuming them — at which point PostHog becomes the better tool, see the revisit triggers in `OBSERVABILITY.md`.
- **Impact / cost:** ~15 LOC (one helper export, ~6 hook call sites). No new dependencies. No backend changes. No env-var changes. Zero ongoing cost (well within Faro free-tier event volume). ~1 hour once the slices to instrument are agreed on. Reversible: deletes cleanly.
