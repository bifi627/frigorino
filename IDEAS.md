# Ideas

Running list of features and improvements we'd like to explore but don't *have* to do. Distinct in intent from `TECH_DEBT.md` — that file holds known issues we consciously deferred; this one holds forward-looking enhancements that came up while working on something else.

Format per item:
- **Title** — one-line hook.
- **Why:** the motivation / user need it serves.
- **Sketch:** rough implementation outline (not exhaustive — a future planning conversation will detail it).
- **Impact / cost:** what changes, rough size.

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

## Async fire-and-forget runner (in-process Channels)

- **Status:** Supersedes a reverted Hangfire trial. Hangfire was implemented (commit `7fb8937`) then **reverted** — its always-on `BackgroundJobServer` polls Postgres continuously (schedule + queue + heartbeat), which keeps the container awake and defeats Railway's serverless sleep (services sleep only after ~10 min with no outbound requests; see `project_no_synthetic_uptime_checks`). Durable, dashboard-backed queueing isn't worth surrendering scale-to-zero on the free tier.
- **Why (still valid):** future features want an async runner — classifying a list item via an LLM (~1s, would block list-add otherwise), OCR-ing receipt photos, sending invite emails. The `MaintenanceHostedService` startup batch covers *periodic* work but not request-triggered fire-and-forget.
- **Direction:** in-process `System.Threading.Channels` queue drained by a single `BackgroundService`. An `IBackgroundTaskQueue` wraps a bounded `Channel<Func<CancellationToken, Task>>`; the consumer dequeues and runs each item in a fresh DI scope with try/catch logging (the canonical ASP.NET Core "queued background tasks" pattern). Event-driven — `WaitToReadAsync` parks at zero CPU when idle, so no polling and no conflict with sleep. Producers (slices / domain-event handlers) inject the queue and enqueue a work item; the item runs in the seconds after the request while the app is still awake.
- **Accepted tradeoff:** in-memory, so work queued-but-not-yet-run is lost on restart/deploy/sleep-eviction. Fine for re-derivable enrichment (re-trigger on the next user action). If a feature genuinely needs durability later, revisit a DB-outbox table drained by the same consumer — not Hangfire.
- **Impact / cost:** ~2-3 small files (queue abstraction + impl, consumer `BackgroundService`), zero new packages (BCL only), no schema. Each future job is ~1 file + an enqueue call.

---

## Promote checked list items into inventory (classifier-driven)

- **Why:** Today the Inventory feature only pays off if items end up in it, and the only way to put them there is manual entry — typing the name, quantity, expiry. That step happens *after* shopping, which is the worst possible time (groceries to put away, kids to feed, etc.), so most users skip it. The expiry tracking that's already wired (colored bars, sort-by-expiry, human-readable countdowns in `InventoryItemContent.tsx`) is invisible to anyone who never adds inventory items. Closing this loop turns Inventory from "another feature you have to maintain" into "the natural other half of the shopping flow."
- **Status (refined 2026-05-29, DDD brainstorm):** split into three independent spec→plan→implement cycles. (1) **Async runner** — prerequisite, now its own spec: `docs/superpowers/specs/2026-05-29-async-channels-runner-design.md`. (2) **Classification engine** (classify + store as metadata) — this entry, decisions below. (3) **Promote-to-inventory UX** — deferred (modal/trigger UX in the Cycle 3 sketch). The original single-blob sketch is superseded by the cycle breakdown below; the OpenAI integration specifics carry over unchanged.
- **Sketch — Cycle 2: classification engine (classify + store as metadata):**
  - **`Product` aggregate (the household's product catalog).** New aggregate root keyed `(HouseholdId, NormalizedName)` unique, cascade-delete with the household. Holds the classification metadata; the household accumulates knowledge about the products it buys. Seed of a richer catalog (later: default unit, storage location, shopping category). Classification is a property of the **product name**, not of a list item — list items / inventory items reference it *by identity* (their current normalized name), so editing an item's text just re-points it (no re-backfill).
  - **`ExpiryProfile` value object.** `ExpiryHandling` enum (`NonPerishable` / `UserEntersFromPackage` / `AiRecommendsShelfLife`) + nullable `ShelfLifeDays`, with the invariant *shelf-life present iff `AiRecommendsShelfLife`, range 1–365* enforced in one factory. Behaviour on the VO: `SuggestedExpiry(today)`. Pure domain helper persisted as flat columns (mirrors the `Quantity` VO spec — **not** an EF owned type).
  - **No denormalization onto `ListItem`.** `ListItem` is left untouched. The toggle / read path consults `Product` by normalized name — a single indexed point lookup on `(HouseholdId, NormalizedName)`. Copying classification onto fluid list items (which users edit) would create a drift/backfill problem and couple the `List` aggregate to data it doesn't own; the lookup is negligible. **(Reversal of the original denorm-hint sketch.)**
  - **`IItemClassifier` port (ACL around the vendor).** In `Frigorino.Domain.Interfaces`; returns `Result<ProductClassification>`. The OpenAI adapter lives in Infrastructure and never leaks vendor types into the domain. Refusal → `NonPerishable` + log; transient error → `Result.Fail` (the job drops it — lossy). A `Version` property stamps `Product.ClassifierVersion`. (Config/DI/settings stay vendor-neutral `Classifier:*`, not `OpenAi:*` — `feedback_vendor_agnostic_by_default`.)
  - **`ProductClassification` composite result (extensibility seam — standing decision).** The port returns a composite type (one field today, `ExpiryProfile Expiry`) rather than a bare `ExpiryProfile`, so future dimensions (storage location, default unit) are additive — one enum + one column + one record field + one schema line, nothing existing rewritten. Multi-dimension stays **typed columns per facet** (not EAV, not a plugin registry until 2–4 facets become many). Per-facet *version* provenance deferred until facets diverge in time/source.
  - **Manual overrides — two-layer model (user always wins).** A `Product` carries an AI-owned `Classification` layer (classifier overwrites it wholesale on (re)classification) and a sparse, user-owned `Override` layer the classifier never touches; consumers read the effective value (`Override ?? Classification`). Ownership is structural — no source flag, no clobber-guard, reversible. **This cycle builds the AI layer only**; the override columns + the "remember this for the product" UI are additive nullable columns later. Per-instance edits (tweaking *this* inventory item's date/quantity in the promote modal) need no modeling — the catalog only suggests. *Learning from corrections* (auto-promoting repeated edits into overrides) is deferred.
  - **Trigger / policy — OPEN.** The policy is "a product name was referenced (item added *or* edited) → ensure it's classified", idempotent + cache-aware (skip if a `Product` row exists at the current `ClassifierVersion`). **Whether it fires via a `ListItemAdded`/`…Edited` domain event + handler, or a direct `IBackgroundTaskQueue.TryEnqueue` from the slice, is intentionally left open** — decide in this cycle (the runner serves both identically; the domain-events infra would be an added prerequisite). `List`↔`Product` eventual consistency (the "race we accept" in Cycle 3) is by-design, not a wart.
  - **Async dispatch.** The classify work runs off the request via the Async runner (Cycle 1): `queue.TryEnqueue((sp, ct) => sp.GetRequiredService<IClassifyProductJob>().Run(householdId, name, ct))`. Lossy on restart by design; re-triggered on the next reference of the same name (cheap once cached).
  - **Normalization v1.** Lowercase + trim + collapse whitespace. **No** stemming / plural-stripping / article-stripping (language-dependent, bilingual en/de). Could graduate to a `NormalizedName` VO later. Revisit if usage shows near-miss duplication.
  - **OpenAI integration specifics.** Picked after a short vendor comparison (top alternatives Gemini 2.5 Flash-Lite and Mistral Small 3 — kept on file as the GDPR / cost-floor fallbacks; reversible).
    - **Model:** `gpt-4.1-nano` — cheapest viable nano-tier with native Structured Outputs (~$0.025 per 1K calls at this shape, ~$1-10/year at expected volume). Re-verify the exact model name string at implementation time; OpenAI renames things.
    - **Strict Structured Outputs**, not JSON mode. `ChatResponseFormat.CreateJsonSchemaFormat(name, schema, isStrict: true)` constrains output during sampling — invalid JSON / off-schema output is impossible. No retry-on-parse-failure path needed. Leftover branch to handle: `message.refusal` (safety) can come back instead of `message.content` — treat as `NonPerishable` and log; near-impossible for inputs of this shape but cheap to cover.
    - **Integration via `Microsoft.Extensions.AI`**, not the raw OpenAI SDK. The implementation depends on `IChatClient` (Microsoft's vendor-neutral chat abstraction) injected from a `ChatClientBuilder` pipeline; swapping to a different provider later is a one-line DI registration change. Side benefit: `.UseOpenTelemetry(loggerFactory, sourceName)` middleware ships token usage + latency + errors into the existing Grafana Cloud OTel pipeline for free, no custom instrumentation.
    - **Packages to pin** (per `feedback_dependency_pinning`): `Microsoft.Extensions.AI` + `Microsoft.Extensions.AI.OpenAI` (provider) + direct pin on transitive `OpenAI`.
    - **Prompt shape.** System message defines the 3 categories with 2-3 bilingual examples each (en + de) — ~150 tokens. User message is just the normalized name. Schema: `expiryHandling` enum + nullable `defaultShelfLifeDays` (1-365), `strict: true`, `additionalProperties: false`. Open door: the same call could also return a category hint (Fridge/Pantry/Freezer) to feed the smart-inventory-target enhancement listed below — defer until a second inventory is the norm.
- **Sketch — Cycle 3: promote-to-inventory UX (deferred):**
  - **Trigger → modal.** When a list item is checked off (`ToggleItemStatus` slice), the response optionally includes a `PromoteSuggestion` built from the `Product` catalog (`EffectiveExpiry.SuggestedExpiry(today)`; `NonPerishable` → no suggestion). Frontend opens a small modal pre-filled; user confirms / tweaks / dismisses. Confirm calls the existing `CreateInventoryItem` slice — no new write endpoint.
  - **Inventory target selection.** Single inventory → skip the picker. 2+ → a dropdown defaulting to the inventory most-recently-promoted-to (per-user preference, store on `User`). Zero-cognition path for single-inventory households (the common case), one tap for multi-.
  - **Frontend.** One new modal component (`PromoteToInventoryModal`) consumed from `features/lists/items/useToggleListItemStatus.ts`'s `onSuccess`. Modal calls the existing `useCreateInventoryItem` hook. No new generated API surface beyond the toggle-response field.
  - **Race we accept.** If the user adds an item AND checks it off in the same ~3s window before classification finishes, the modal won't fire that one time. Cached on every subsequent reference of the same name. Not worth a real-time push channel for v1 (this is `List`↔`Product` eventual consistency, by design).
  - **Reqnroll scenarios.** Happy path (perishable item, classifier ran, user promotes). Edge: non-perishable item, modal never fires. Edge: classification not yet complete, modal doesn't fire (documented eventual consistency).
- **Impact / cost:** Three cycles. **(1) Async runner** — ~4 files, BCL only, no schema (separate spec). **(2) Classification engine** — `Product` aggregate + `ExpiryProfile` VO + `IItemClassifier` port + OpenAI adapter via `Microsoft.Extensions.AI` (~3 packages) + 1 DI extension + the classify job/policy + 1 EF migration (new `Product` table; `ListItem` untouched). **(3) Promote UX** — 1 modified toggle slice (suggestion field) + 1 modal + the inventory-target preference. Running cost is rounding error at this volume (~$1–10/year). Future doors deliberately left open: storage / unit facets via the `ProductClassification` composite; `ClassifierVersion` re-classification when the prompt is tuned; the user-override layer; the same `Product` catalog powering inventory-side enrichment. If OpenAI ever needs swapping (cost / availability / EU residency push), the `IItemClassifier` / `IChatClient` layer makes it a one-line DI change.

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

---

## Rework user invite to household

- **Status:** placeholder — needs a dedicated brainstorming session before any sketch. This entry just captures the scenarios so they aren't lost.
- **Why:** Adding a member today (`AddMember.cs`) resolves an *existing* user and attaches them to the household. That only covers the case where the invitee already has an account and is already known. A real invite flow has to handle people who aren't users yet and needs an actual delivery mechanism so the invite reaches them.
- **Scenarios to work through:**
  1. **Invited user already exists** — current path; resolve and add (or send them an in-app/notification invite to accept rather than auto-joining?).
  2. **Invited user does not exist yet** — no `User` row to attach to. Pending-invite record keyed by email/identifier, redeemed when they sign up and first log in? Ties into [[First-run onboarding: dedicated "create your first household" page]] as a parallel onboarding entry point (invite-acceptance vs. create-first-household).
  3. **Other flows (TBD):** re-invite / revoke a pending invite, invite expiry, role chosen at invite time, declining an invite, inviting someone already in the household.
- **Open question — delivery:** how does the invitee actually receive the invite? Options to weigh later: email (needs an email sender — none wired today), shareable invite link/code, in-app notification for existing users. Keep vendor-neutral per [[Async fire-and-forget runner (in-process Channels)]] if email/push is involved (sender behind an interface).
- **Impact / cost:** unknown until brainstormed — likely a new pending-invite entity, new slices (create/accept/revoke invite), and a delivery mechanism. Defer sizing to the planning conversation.

---

## Quantity as a domain value object (value + unit)

- **Spec:** fully designed — see [`docs/superpowers/specs/2026-05-27-quantity-value-object-design.md`](docs/superpowers/specs/2026-05-27-quantity-value-object-design.md). This entry is just the pointer; the spec is authoritative.
- **Why:** `Quantity` is a free-text `string?` today on both `ListItem` and `InventoryItem` (`Frigorino.Domain/Entities/ListItem.cs:13`, `InventoryItem.cs:16`). A user types "2 kg" or "500ml" and it's opaque to the app — we can't sum, compare, convert, or reason about it. Modelling it as a proper value object (a numeric value + a unit) unlocks real features later: "you have 1.5kg, recipe needs 200g", merging duplicate inventory items, low-stock thresholds, shopping-list math.
- **Sketch (headline decisions):** `Quantity` value object (`decimal Value` + fixed `QuantityUnit` enum: g/kg, ml/l, piece/pack/can/bottle/bag) as a **pure domain helper, not an EF owned/complex type**. Two flat nullable columns per item (`QuantityValue` + `QuantityUnit`, both-or-null), replacing the string column — aligns with [[feedback_flat_db_schema]]. **Strictly value+unit going forward** (no free-text escape hatch); a redesigned structured picker (grouped unit chips, suggestion-ready row) produces clean data at entry time. A reusable `Quantity.TryParse` powers the migration backfill; **unparseable legacy values are dropped** (accepted data-loss tradeoff, incl. stage/prod).
- **Scope / sequencing:** v1 = data model + migration + DTOs + picker + formatted display. **Planning deferred** — sequence **after the classifier** ([[Promote checked list items into inventory (classifier-driven)]]) so the picker can offer item-aware unit suggestions and inline text understanding ("2 apples", "milk 500ml") lands with a real consumer. Conversions / duplicate-merge / low-stock thresholds are explicit follow-ups.
- **Impact / cost:** one value object + parser + EF migration/backfill, two flat columns per item, touches both item aggregates and their create/update slices + responses, plus the composer `quantityFeature` rework + display. Medium. Mostly enabling work — payoff features are separate follow-ups once the data is structured.
