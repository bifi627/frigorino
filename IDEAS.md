# Ideas

Running list of features and improvements we'd like to explore but don't *have* to do. Distinct in intent from `TECH_DEBT.md` — that file holds known issues we consciously deferred; this one holds forward-looking enhancements that came up while working on something else.

Format per item:
- **Title** — one-line hook.
- **Why:** the motivation / user need it serves.
- **Sketch:** rough implementation outline (not exhaustive — a future planning conversation will detail it).
- **Impact / cost:** what changes, rough size.

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
    - Explicit **bypass** for: `/api/*`, `/openapi/*`, `/scalar/*`. Either via a higher-priority bypass rule or by naming the cacheable paths positively and leaving the rest at default.
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
    - HTTP request timeout (e.g. Cloudflare free is 100s, others vary): no current endpoint exceeds anything reasonable. Track if a long-poll/SSE endpoint is added.
  - **Stage + prod:** same proxy pattern with separate subdomains/origins. Cache rules scope per hostname on every provider.
- **Impact / cost:** zero application code changes — the cache headers this idea relies on already ship from origin. Setup is mostly dashboard work plus one purge step in the deploy workflow. ~4-8 hours total including learning the provider's rule UI, testing the stale-window behavior, and wiring the deploy purge. Cloudflare free plan covers everything at zero cost; other providers cents/GB. Reversible — if it goes wrong, flip the proxy off and DNS reverts to direct-to-Railway within minutes.

---

## Async fire-and-forget runner (in-process Channels)

- **Status:** Supersedes a reverted Hangfire trial. Hangfire was implemented (commit `7fb8937`) then **reverted** — its always-on `BackgroundJobServer` polls Postgres continuously (schedule + queue + heartbeat), which keeps the container awake and defeats Railway's serverless sleep (services sleep only after ~10 min with no outbound requests; see `project_no_synthetic_uptime_checks`). Durable, dashboard-backed queueing isn't worth surrendering scale-to-zero on the free tier.
- **Why (still valid):** future features want an async runner — classifying a list item via an LLM (~1s, would block list-add otherwise), OCR-ing receipt photos, sending invite emails. The `MaintenanceHostedService` startup batch covers *periodic* work but not request-triggered fire-and-forget.
- **Direction:** in-process `System.Threading.Channels` queue drained by a single `BackgroundService`. An `IBackgroundTaskQueue` wraps a bounded `Channel<Func<CancellationToken, Task>>`; the consumer dequeues and runs each item in a fresh DI scope with try/catch logging (the canonical ASP.NET Core "queued background tasks" pattern). Event-driven — `WaitToReadAsync` parks at zero CPU when idle, so no polling and no conflict with sleep. Producers (slices / domain-event handlers) inject the queue and enqueue a work item; the item runs in the seconds after the request while the app is still awake.
- **Accepted tradeoff:** in-memory, so work queued-but-not-yet-run is lost on restart/deploy/sleep-eviction. Fine for re-derivable enrichment (re-trigger on the next user action). If a feature genuinely needs durability later, revisit a DB-outbox table drained by the same consumer — not Hangfire.
- **Impact / cost:** ~2-3 small files (queue abstraction + impl, consumer `BackgroundService`), zero new packages (BCL only), no schema. Each future job is ~1 file + an enqueue call.

---

## Promote checked list items into inventory (classifier-driven)

- **Why:** Today the Inventory feature only pays off if items end up in it, and the only way to put them there is manual entry — typing the name, quantity, expiry. That step happens *after* shopping, which is the worst possible time (groceries to put away, kids to feed, etc.), so most users skip it. The expiry tracking that's already wired (colored bars, sort-by-expiry, human-readable countdowns in `InventoryItemContent.tsx`) is invisible to anyone who never adds inventory items. Closing this loop turns Inventory from "another feature you have to maintain" into "the natural other half of the shopping flow."
- **Sketch:**
  - **Trigger.** When a list item is checked off (`ToggleItemStatus` slice), the response optionally includes a `PromoteSuggestion`. Frontend opens a small modal pre-filled with classifier output. User confirms / tweaks / dismisses. Confirm calls the existing `CreateInventoryItem` slice — no new write endpoint.
  - **Classifier shape (vendor-agnostic).** `IClassificationService` in `Frigorino.Domain.Interfaces` returns one of three `ExpiryHandling` modes: `NonPerishable` (skip the modal entirely — toothpaste, batteries), `UserEntersFromPackage` (open modal, empty date field, "check the package" hint — milk, packaged meat), `AiRecommendsShelfLife` (open modal, date pre-filled to `today + DefaultShelfLifeDays` — fresh produce, baked goods). Vendor selected: **OpenAI** (see next bullet for specifics); interface stays neutral so a future swap is a one-line DI change. Config keys, DI extension, and settings stay vendor-neutral (`Classifier:*`, not `OpenAi:*`) — see `feedback_vendor_agnostic_by_default` in user memory.
  - **OpenAI integration specifics.** Picked after a short vendor comparison (top alternatives Gemini 2.5 Flash-Lite and Mistral Small 3 — kept on file as the GDPR / cost-floor fallbacks; reversible).
    - **Model:** `gpt-4.1-nano` — cheapest viable nano-tier with native Structured Outputs (~$0.025 per 1K calls at this shape, ~$1-10/year at expected volume). Re-verify the exact model name string at implementation time; OpenAI renames things.
    - **Strict Structured Outputs**, not JSON mode. `ChatResponseFormat.CreateJsonSchemaFormat(name, schema, isStrict: true)` constrains output during sampling — invalid JSON / off-schema output is impossible. No retry-on-parse-failure path needed. Leftover branch to handle: `message.refusal` (safety) can come back instead of `message.content` — treat as `NonPerishable` and log; near-impossible for inputs of this shape but cheap to cover.
    - **Integration via `Microsoft.Extensions.AI`**, not the raw OpenAI SDK. The implementation depends on `IChatClient` (Microsoft's vendor-neutral chat abstraction) injected from a `ChatClientBuilder` pipeline; swapping to a different provider later is a one-line DI registration change. Side benefit: `.UseOpenTelemetry(loggerFactory, sourceName)` middleware ships token usage + latency + errors into the existing Grafana Cloud OTel pipeline for free, no custom instrumentation.
    - **Packages to pin** (per `feedback_dependency_pinning`): `Microsoft.Extensions.AI` + `Microsoft.Extensions.AI.OpenAI` (provider) + direct pin on transitive `OpenAI`.
    - **Prompt shape.** System message defines the 3 categories with 2-3 bilingual examples each (en + de) — ~150 tokens. User message is just the normalized name. Schema: `expiryHandling` enum + nullable `defaultShelfLifeDays` (1-365), `strict: true`, `additionalProperties: false`. Open door: the same call could also return a category hint (Fridge/Pantry/Freezer) to feed the smart-inventory-target enhancement listed below — defer until a second inventory is the norm.
  - **Async dispatch.** Classifying takes ~1s and shouldn't block list-add. `CreateItem` enqueues a `ClassifyItem` work item onto the in-process `System.Threading.Channels` queue (see [[Async fire-and-forget runner (in-process Channels)]] — build that runner first). Lossy on restart by design; re-triggered on the next add of the same name, which the per-household knowledge cache below makes cheap.
  - **Knowledge cache (per household).** New `ItemKnowledge` entity: `(HouseholdId, NormalizedName)` unique → `ExpiryHandling`, `DefaultShelfLifeDays?`, `ClassifiedAt`, `ClassifierVersion`. The classifier job is cache-aware: skip if a row exists, otherwise call the vendor and write. Knowledge is **household-scoped**, not global — same name can be classified differently per household, and there's no cross-household exposure. Cascade-delete with the household.
  - **Denorm hint on `ListItem`.** Add nullable `ExpiryHandling?` + `DefaultShelfLifeDays?` to `ListItem` so the hot-path toggle handler doesn't need an extra read. Classifier job's last step: backfill these fields on every matching `ListItem` in the household. New list items added before classification finishes stay null (toggle silent) — see "race we accept" below.
  - **Normalization v1.** Lowercase + trim + collapse whitespace. **No** stemming / plural-stripping / article-stripping in v1 — language-dependent (app is bilingual en/de) and adds risk for marginal wins. Revisit if usage shows near-miss duplication.
  - **Inventory target selection.** If the household has exactly one inventory, the modal skips the picker and uses it. With 2+, show a dropdown defaulting to the inventory most-recently-promoted-to (per-user preference, store on `User`). Zero-cognition path for single-inventory households (the common case), one tap for multi-.
  - **Race we accept.** If the user adds an item AND checks it off in the same ~3s window before classification finishes, the modal won't fire that one time. Cached on every subsequent add of the same name. Not worth a real-time push channel for v1.
  - **Frontend.** One new modal component (`PromoteToInventoryModal`) consumed from `features/lists/items/useToggleListItemStatus.ts`'s `onSuccess`. Modal calls the existing `useCreateInventoryItem` hook. No new generated API surface beyond the toggle-response field.
  - **Reqnroll scenarios.** Happy path (perishable item, classifier runs, user promotes). Edge: non-perishable item, modal never fires. Edge: classifier hasn't completed yet, modal doesn't fire (race acceptance documented).
- **Impact / cost:** Two layers on top of the Channels-runner prerequisite: (1) Classifier abstraction + OpenAI implementation via `Microsoft.Extensions.AI` — small, ~3 packages + 1 DI extension + 1 service file; (2) Feature itself — 1 new entity + 2 nullable columns + 1 new job + 1 new modal + 1 modified toggle slice + 1 new `ItemKnowledge` cache patch step. Running cost is rounding error at this volume (~$1-10/year). Future doors deliberately left open: classifier could also return a storage hint (Fridge/Pantry/Freezer) to make multi-inventory target selection smarter; `ClassifierVersion` lets us re-classify when the prompt is tuned; same `IClassificationService` could power inventory-side enrichment later. If OpenAI ever needs swapping (cost / availability / EU residency push), the `IChatClient` layer makes it a one-line DI change.

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
  - Backend-emitted events. Background tasks and other backend paths don't need to push Faro events; backend behaviour is already covered by OTel traces.
  - Cross-tool bridging (Faro events → Loki). They live in the Faro app inside Grafana Cloud; no bridging needed at this scale.
  - Custom funnels / retention curves. Grafana's Frontend Observability standard cuts cover event volume by user / env. DIY LogQL funnel queries are possible but only worth building when a product team starts consuming them — at which point PostHog becomes the better tool, see the revisit triggers in `OBSERVABILITY.md`.
- **Impact / cost:** ~15 LOC (one helper export, ~6 hook call sites). No new dependencies. No backend changes. No env-var changes. Zero ongoing cost (well within Faro free-tier event volume). ~1 hour once the slices to instrument are agreed on. Reversible: deletes cleanly.

---

## First-run onboarding: dedicated "create your first household" page

- **Why:** A user who signs in for the first time has zero households. Today the `_protected` layout assumes an active household exists — `CurrentHouseholdService` falls back to "highest-role / earliest-joined", which returns nothing, and the UI lands in an awkward empty state where most navigation is meaningless. New users need a single, obvious next step ("create a household") rather than a dashboard full of dead links. This is especially load-bearing once we start onboarding real users beyond the dev/stage tester pool.
- **Sketch:**
  - Detect the zero-household case on app boot — `useUserHouseholds()` returns `[]` after auth resolves.
  - Route guard inside `_protected`: if households-list is loaded and empty, redirect to `/onboarding` (or `/household/create` reused with onboarding mode) instead of rendering the normal shell. Avoid rendering the household switcher / sidebar when there's nothing to switch to.
  - Onboarding page is a single-purpose view: friendly copy, one form (household name), submit → `useCreateHousehold` → on success, set active household + redirect to the normal app shell. No "skip" — the app is unusable without a household.
  - Edge case: user removed from their last household while session is live → same flow re-engages (next navigation lands on onboarding). Worth verifying.
  - Consider whether invite-acceptance is a parallel onboarding entry point (user invited before they signed up): out of scope for v1, but the page copy shouldn't preclude it ("Create a household, or wait for an invite to be accepted" — TBD).
- **Impact / cost:** one new route + page component (~50 LOC), one `_protected` guard change (~10 LOC), no backend changes (the empty-list signal already exists). Mostly a UX/copy exercise. ~half-day including i18n strings (en + de).

---

## Undo on item delete (snackbar with revert)

- **Why:** Delete is a one-tap action on list items and inventory items, and mistakes happen — fat-finger on mobile, wrong row, regret a second later. Today the only recovery is "type it back in", which loses any metadata (sort order, classification, quantity, original `CreatedAt`). A "deleted — undo" snackbar is the standard mobile-first UX for destructive single actions, and given entities already use `IsActive` soft-delete (managed centrally in `ApplicationDbContext`), the backend cost is just exposing a restore endpoint.
- **Sketch:**
  - **Backend:** add `POST /api/households/{hh}/lists/{l}/items/{id}/restore` (and inventory equivalent) as a vertical slice — flips `IsActive` back to true, returns the restored DTO. Aggregate method: `List.RestoreItem(itemId)` / `Inventory.RestoreItem(itemId)` returning `Result<ListItem>` with `EntityNotFoundError` if no soft-deleted row exists. Permission: same as delete.
  - **Frontend:** mutation hook `useRestoreListItem` / `useRestoreInventoryItem` following the existing arg-less mutation pattern. The delete mutation's `onSuccess` triggers a MUI `Snackbar` (existing in the codebase or add via `notistack`) with an "Undo" action that calls the restore mutation with the deleted item's id. On undo success, invalidate the list/inventory query so the item reappears in its original sort position.
  - **Snackbar shape:** ~5s auto-dismiss, single-line ("Item deleted") + "UNDO" button. Stacks if multiple deletes happen rapidly — each undo restores its own item. Translatable via existing i18n setup.
  - **Open questions:**
    - Sort order on restore: does the item return to its original `SortOrder` value (preserved on the row), or get appended to the bottom? Lean toward preserving the original — minimizes surprise.
    - TTL for "undoable" deletes: today soft-deletes live forever. Should restore work indefinitely (anyone with the item id can resurrect) or only within the snackbar window? Lean indefinitely — the snackbar is just the UX entry point; a future "trash bin" view could surface older soft-deletes.
    - Scope: items only for v1. Restoring a deleted *list* or *inventory* (parent aggregates) is a bigger conversation — cascades, child item state, who's authorized — and warrants its own idea.
- **Impact / cost:** two new slices + two aggregate methods (~80 LOC backend), two new mutation hooks + snackbar wiring (~60 LOC frontend), i18n strings. ~half-day. Reversible. Pairs naturally with the Lists/Inventories vertical-slice migration — do it as part of that work rather than retrofitting the legacy controllers.

---

## Stale checked-item cleanup: schedule it + extend to inventories

- **What already exists:** `Frigorino.Infrastructure.Tasks.DeleteInactiveItems` (`Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs`) already hard-deletes soft-deleted Households/Inventories/Lists/InventoryItems and **checked `ListItems` where `UpdatedAt < UtcNow - 30 days`**. It's wired through `AddMaintenanceServices` (`Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs`) and runs **once on app startup** via `MaintenanceHostedService` (a `BackgroundService` with a 5s delay). So the *idea* is half-done; the gaps below are what's missing.
- **Why this needs follow-up:**
  - **Triggered by deploys, not by time.** On Railway with sleep mode, the app may stay warm for days or sleep for days — cleanup cadence is unpredictable. Active users on a long-warm instance build up checked-item cruft until the next deploy.
  - **No equivalent rule for `InventoryItems`.** Inventories often have items "consumed" (checked) that linger forever. The same staleness signal applies.
  - **30-day threshold is a magic number** baked into the cleanup file. Should be a configurable setting (`Maintenance:StaleCheckedItemDays`) so households with different rhythms can tune it later (out of scope: per-household setting).
- **Sketch:**
  - Extend the query to cover `InventoryItems` with the same `IsChecked && UpdatedAt < threshold` rule. Verify `InventoryItem` actually has a `Status`/checked column — if it doesn't, the inventory side of this idea needs a separate "items have a consumed state" discussion first.
  - Move the threshold into `appsettings.json` (`Maintenance:StaleCheckedItemDays`, default 30). Read via `IOptions<MaintenanceOptions>`.
  - Both changes stay inside the existing `DeleteInactiveItems` task / `MaintenanceHostedService` startup batch. (Hangfire was trialled then reverted — see [[Async fire-and-forget runner (in-process Channels)]] — so there's no scheduler to migrate to; the startup batch is the sleep-safe home.)
  - **Pairs with [[Undo on item delete]]:** if users can restore checked items via a snackbar, a 30-day hard-delete is fine (snackbar window is seconds, restore via "trash bin" is a separate future feature). If a trash bin lands later, this cleanup becomes the trash-bin TTL enforcer.
- **Impact / cost:** ~half-day. Extend `DeleteInactiveItems` to cover `InventoryItems` and move the 30-day threshold into `appsettings.json` (`Maintenance:StaleCheckedItemDays`, via `IOptions`). No new packages, no schema change, no Hangfire. Reversible — the cleanup is a `DELETE` by predicate; narrow it if the rule is wrong.

---

## Rich list items: photos & documents (text / image / document)

- **Spec:** fully designed — see [`docs/superpowers/specs/2026-05-23-rich-list-items-design.md`](docs/superpowers/specs/2026-05-23-rich-list-items-design.md). This entry is just the pointer; the spec is authoritative.
- **Why:** Items are text-only today. Users want to attach a **photo they took** or a **document** (PDF manual/warranty/receipt) to a list, while the item still checks off, reorders, and soft-deletes like any other. The existing URL-in-text image/link hack (`ListItemContent.tsx`) doesn't cover real uploads.
- **Sketch (headline decisions):** typed `ListItem` (`Type ∈ {Text, Image, Document}`) on a **single flat table** — nullable file columns, **no EF inheritance**. One coupled file pipeline behind a vendor-neutral `IFileStorage` port (proxy upload through the API, which stays the authz gatekeeper since membership lives in Postgres, not Firebase rules). Thumbnails via ImageSharp. Three per-type frontend renderers + a WhatsApp-style attach affordance.
- **Scope / sequencing:** v1 ships the seam + a `LocalFileStorage` dev backend (demoable/testable locally). Production backend (Firebase-GCS / R2 / S3) is a **separate follow-up** — Firebase Storage now requires the Blaze plan (stays $0 under free limits). Post-upload classify/analyze and orphaned-blob cleanup are future backend hooks.
- **Dependencies:** restore behavior leans on [[Undo on item delete]]'s `RestoreItem`, not yet in `stage`. A future classify hook would build on [[Async fire-and-forget runner (in-process Channels)]] and reuse the [[Promote checked list items into inventory (classifier-driven)]] classifier.
- **Impact / cost:** one EF migration (1 enum + 5 nullable columns), ~3 new slices + 1 aggregate method, `IFileStorage` + dev impl + ImageSharp, three renderers + upload/blob hooks. Production storage backend is additional, separately scoped.
