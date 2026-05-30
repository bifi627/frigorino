# AI item classification engine (Cycle 2) — design

- **Date:** 2026-05-30
- **Status:** Approved (design)
- **Branch:** `feat/classification-engine` (off `stage`)
- **Predecessor:** Cycle 1 async Channels runner (`IBackgroundTaskQueue`), merged to `stage` (`a894f4c`).
- **Successor:** Cycle 3 promote-to-inventory UX (deferred — see `IDEAS.md`).

## Summary

When a user references a product by name on a list (adds or edits a list item), the app should learn
how that product expires — non-perishable, user-reads-the-package, or AI-suggests-a-shelf-life — so a
later feature (Cycle 3) can pre-fill an inventory entry when the item is checked off.

This cycle builds the **classification engine only**: a per-household **product catalog** (`Product`
aggregate keyed `(HouseholdId, NormalizedName)`), a vendor-isolated **classifier** (`IItemClassifier`
port → OpenAI adapter), and the **async classify job** that the list-item slices enqueue onto the
Cycle 1 runner. The feature is **invisible** — no new endpoint, no DTO change, no UI, no client
regeneration. It writes classification metadata that Cycle 3 will consume.

The feature is **fully optional**: with no classifier key configured, it is a no-op (app, dev-up, CI,
and the full test suite run with zero AI config).

## Goals

- A `Product` catalog aggregate per household, keyed by normalized name, holding classification metadata.
- An `ExpiryProfile` value object (handling + optional shelf-life) with a single enforced invariant.
- A slim domain port `IItemClassifier` returning `Result<ProductClassification>` — the **only** AI
  abstraction; the OpenAI SDK never leaks past it.
- An idempotent, cache-aware classify job run off the request thread via the Cycle 1 runner.
- Graceful no-op when the classifier is not configured.
- No change to `ListItem`, no new API surface, no frontend work.

## Non-goals / out of scope (deferred, designed-for)

- **User override layer** (the sparse, user-owned columns + "remember this for the product" UI). The
  effective-value model is `Override ?? Classification`; **this cycle builds the AI `Classification`
  layer only**. Override columns are additive nullable columns later.
- **Promote-to-inventory UX** (Cycle 3): the toggle-time suggestion, modal, inventory-target picker.
- **Inventory-item triggers.** Only list-item add/edit triggers classification this cycle. Adding the
  inventory side later is a one-line trigger call per slice.
- **Extra classification facets** (storage location, default unit). The composite-result seam
  (`ProductClassification`) is built so these are additive; none are built now.
- **Learning from corrections** (auto-promoting repeated user edits into overrides).
- **Per-facet version provenance** (single `ClassifierVersion` on the product for now).
- **Domain-event infrastructure.** The trigger is a direct enqueue (decision 6); domain events are a
  later refactor if a second subscriber justifies them.
- **Multi-vendor abstraction (`IChatClient`/Microsoft.Extensions.AI).** Deliberately not used — one
  slim port (`IItemClassifier`) is the swap point (decision 5).

## Key decisions & rationale

1. **`Product` is a new aggregate root keyed `(HouseholdId, NormalizedName)`, cascade-deleted with the
   household.** Classification is a property of the *product name*, not of a list item — items
   reference it by identity (their current normalized name). Editing an item's text just re-points the
   lookup; no backfill. Seed of a richer household catalog later (default unit, storage, category).

2. **No denormalization onto `ListItem`.** `ListItem` is untouched. The read path (Cycle 3) consults
   `Product` by normalized name — a single indexed point lookup on `(HouseholdId, NormalizedName)`.
   Copying classification onto fluid, user-edited list items would create a drift/backfill problem and
   couple the `List` aggregate to data it doesn't own.

3. **`ExpiryProfile` is a pure domain value object, flat-persisted — not an EF owned/complex type.**
   Mirrors the `Quantity` VO spec: a readonly record struct used for validate/suggest, stored as flat
   nullable columns on `Product`. Sidesteps EF nullable-complex-type limits; matches the flat-schema
   preference.

4. **`ProductClassification` is a composite result, not a bare `ExpiryProfile`.** The port returns a
   record wrapping the profile (one field today, `Expiry`) so future facets are additive — one enum +
   one column + one record field + one schema line, nothing existing rewritten. Multi-facet stays
   **typed columns per facet** (not EAV, not a plugin registry).

5. **One slim abstraction: `IItemClassifier` (domain port). The OpenAI SDK is used directly behind
   it — no `IChatClient`/Microsoft.Extensions.AI layer.** (User decision: don't over-build layered
   abstractions; a slim interface makes a future vendor refactor slim too.) The domain only ever sees
   `Result<ProductClassification>`; no AI vendor type crosses into Domain/Features. Swapping vendor
   later = rewrite the one Infra adapter behind the unchanged port; worst case (a vendor with no good
   SDK fit) is still a single Infra class. Config keys stay vendor-neutral `Classifier:*` (not
   `OpenAi:*`) per the vendor-agnostic rule.

6. **Trigger = direct enqueue from the list-item slices, via a thin `IProductClassificationTrigger`
   seam.** (User decision: pragmatic first; decouple with domain events later only if a second
   subscriber appears.) `CreateItem`/`UpdateItem` call `trigger.OnProductReferenced(householdId, text)`
   after `SaveChangesAsync`; the slice knows nothing about the queue, the job, or the config. The
   enabled impl enqueues onto the Cycle 1 runner; the disabled impl is a no-op. This seam is the
   localized swap point if we move to domain events later.

7. **Graceful no-op when unconfigured (optional feature).** `Classifier:Enabled=false` (or absent key)
   registers the no-op trigger; nothing is enqueued, nothing is written. App, dev-up, CI, and the full
   test suite run with no AI secret. The build-time OpenAPI run sees `Enabled=false` → safe (no special
   `isBuildTimeOpenApi` gating needed).

8. **List-items-only trigger this cycle.** `CreateItem` always; `UpdateItem` only when the text
   actually changed (a quantity/status-only edit doesn't re-classify). Inventory-item triggers deferred.

9. **OpenAI specifics.** Model `gpt-4.1-nano` (cheapest nano tier with native Structured Outputs;
   ~$1–10/year at expected volume — re-verify the exact model string at implementation, OpenAI
   renames). **Strict Structured Outputs**, not JSON mode: invalid/off-schema output is impossible at
   sampling time, so no retry-on-parse path. Refusal (`message.refusal`) → treat as `NonPerishable` +
   log. Transient/API error → `Result.Fail` (the job drops it — lossy by design, re-triggered on the
   next reference). A `Version` constant stamps `Product.ClassifierVersion`.

10. **Normalization v1.** Lowercase + trim + collapse internal whitespace. **No** stemming /
    plural-stripping / article-stripping (language-dependent, bilingual en/de). One source of truth
    (`ProductName.Normalize`). Could graduate to a `NormalizedName` VO later.

11. **Idempotency + concurrency.** The job skips when a `Product` exists at `ClassifierVersion >=
    classifier.Version` (cache hit). Two rapid references to a brand-new name can both miss the cache
    and race to insert; the unique `(HouseholdId, NormalizedName)` index arbitrates and the job
    **catches `DbUpdateException` as benign** (the other writer won; one wasted ~$0.00002 call is
    accepted).

## Domain model (`Frigorino.Domain`)

- **`ExpiryHandling` enum:** `NonPerishable`, `UserEntersFromPackage`, `AiRecommendsShelfLife`.
  Serializes as integer on the wire by the existing convention (no string converter) — though nothing
  in this cycle puts it on the wire.
- **`ExpiryProfile` readonly record struct:** `ExpiryHandling Handling`, `int? ShelfLifeDays`.
  - `const int ShelfLifeDaysMin = 1; const int ShelfLifeDaysMax = 365;`
  - `Create(handling, shelfLifeDays) → Result<ExpiryProfile>`: invariant *`ShelfLifeDays` is set iff
    `Handling == AiRecommendsShelfLife`, and within 1–365 when set*. Returns `Result.Fail` with a
    `Property`-tagged error otherwise.
  - `static ExpiryProfile NonPerishable` convenience.
  - `SuggestedExpiry(DateOnly today) → DateOnly?` = `today.AddDays(ShelfLifeDays.Value)` only for
    `AiRecommendsShelfLife`; otherwise `null`.
- **`ProductClassification` record:** `ProductClassification(ExpiryProfile Expiry)` — composite seam.
- **`ProductName` static:** `Normalize(string raw) → string` (lowercase, trim, collapse whitespace).
- **`Product` aggregate (`Frigorino.Domain/Entities/Product.cs`):**
  - `int Id`, `int HouseholdId`, `string NormalizedName` (`const int NormalizedNameMaxLength = 200`),
    AI-layer columns `ExpiryHandling ClassificationExpiryHandling` + `int? ClassificationShelfLifeDays`,
    `int ClassifierVersion`, `DateTime CreatedAt`, `DateTime UpdatedAt`.
  - `static Create(householdId, normalizedName, ProductClassification, version) → Result<Product>`
    (validates household id, non-empty/length-bounded normalized name).
  - `ApplyClassification(ProductClassification, version)` — overwrites the AI layer wholesale +
    re-stamps `ClassifierVersion` (mostly assignment; `UpdatedAt` auto-stamped by the context).
  - `EffectiveExpiry` (read) — reconstructs `ExpiryProfile` from the AI columns. **Minimal now**
    (returns the Classification profile); becomes `OverrideExpiry ?? ClassificationExpiry` when override
    columns land. No override columns this cycle.

## Ports (`Frigorino.Domain/Interfaces`)

- **`IItemClassifier`:** `Task<Result<ProductClassification>> ClassifyAsync(string normalizedName,
  CancellationToken ct);` and `int Version { get; }`.
- **`IClassifyProductJob`:** `Task Run(int householdId, string rawName, CancellationToken ct);` — the
  enqueued unit of work (the runner resolves it in a fresh scope).
- **`IProductClassificationTrigger`:** `void OnProductReferenced(int householdId, string rawName);` —
  the seam the slices call.

## Infrastructure (`Frigorino.Infrastructure`)

- **`OpenAiItemClassifier : IItemClassifier`** — uses the official `OpenAI` .NET SDK directly. Builds a
  strict JSON-schema response format (`expiryHandling` enum + nullable `defaultShelfLifeDays` 1–365,
  `additionalProperties:false`, strict). System prompt defines the 3 categories with 2–3 **bilingual**
  (en + de) examples each (~150 tokens); user message is the normalized name. Maps the response →
  `ProductClassification(ExpiryProfile.Create(...).Value)`. Refusal/off-schema → `NonPerishable` + log
  warning; transient/exception → `Result.Fail`. Logs latency/outcome via `ILogger` (richer OTel
  deferred). `Version` is a constant (start at `1`).
- **`ClassifyProductJob : IClassifyProductJob`** (scoped). Flow:
  1. `normalized = ProductName.Normalize(rawName)`; empty → return.
  2. Look up `Product` by `(HouseholdId, NormalizedName)`.
  3. If found and `ClassifierVersion >= classifier.Version` → return (cache hit).
  4. `result = await classifier.ClassifyAsync(normalized, ct)`; `IsFailed` → log + return (drop).
  5. Found → `ApplyClassification`; else → `Product.Create(...)` + `db.Products.Add`.
  6. `try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { /* benign unique race */ log; }`.
- **`QueueingProductClassificationTrigger : IProductClassificationTrigger`** — injects
  `IBackgroundTaskQueue`; `TryEnqueue((sp, ct) => sp.GetRequiredService<IClassifyProductJob>()
  .Run(householdId, rawName, ct))`.
- **`NullProductClassificationTrigger : IProductClassificationTrigger`** — no-op.
- **`AddItemClassification(IConfiguration)` DI extension:** registers `IClassifyProductJob`
  (scoped). If `Classifier:Enabled` and a key is present → register `OpenAiItemClassifier` +
  `QueueingProductClassificationTrigger`; else → `NullProductClassificationTrigger`. Wired from
  `Program.cs` (`builder.Services.AddItemClassification(builder.Configuration)`), alongside the other
  Infrastructure DI extensions. Must not throw when the key is absent.
- **Config (vendor-neutral):** `Classifier:Enabled` (bool), `Classifier:ApiKey`, `Classifier:Model`
  (default `gpt-4.1-nano`). Empty placeholders in `appsettings.json`; real values via user-secrets /
  env / Railway.

## Slices (`Frigorino.Features/Lists/Items`)

- **`CreateItem`** — after `SaveChangesAsync`, `trigger.OnProductReferenced(householdId, request.Text)`.
- **`UpdateItem`** — same call, **only when the text field actually changed** (skip on
  quantity/status-only edits). The slice injects `IProductClassificationTrigger`; no other change.
- No request/response/DTO changes. No OpenAPI/client regeneration.

## Persistence & migration

- **`Product` EF config** (`IEntityTypeConfiguration<Product>`, auto-applied by
  `ApplyConfigurationsFromAssembly`): `Id` `ValueGeneratedOnAdd`; `HouseholdId` required;
  `NormalizedName` `HasMaxLength(Product.NormalizedNameMaxLength)` required; `ClassificationExpiryHandling`
  required; `ClassificationShelfLifeDays` nullable; `ClassifierVersion` required; `CreatedAt`/`UpdatedAt`
  required; **unique index `(HouseholdId, NormalizedName)`**; index on `HouseholdId`; cascade from
  `Household` via `.HasOne<Household>().WithMany().HasForeignKey(p => p.HouseholdId)
  .OnDelete(DeleteBehavior.Cascade)` (**no navigation added to `Household`** — keeps it untouched).
- `DbSet<Product> Products` on `ApplicationDbContext`; add `Product` branches to the timestamp stamping
  in `SaveChangesAsync` (Added → `CreatedAt`/`UpdatedAt`; Modified → `UpdatedAt`).
- One migration `AddProductCatalog`, applied automatically at startup via `MigrateAsync()`.

## Packages

- **`OpenAI`** (official .NET SDK) — exact-pinned (per the dependency rule; re-verify latest stable at
  implementation). No other new packages. No `Microsoft.Extensions.AI*`.
- `Application/Dockerfile` unaffected (no new project); `docker build` at the end as a drift check.

## Testing

- **Domain unit (`Frigorino.Test`):** `ExpiryProfile.Create` invariant (shelf-life iff
  `AiRecommendsShelfLife`, range 1–365, both directions) + `SuggestedExpiry`; `ProductName.Normalize`
  (case, trim, internal whitespace, en/de inputs); `Product.Create`/`ApplyClassification`/
  `EffectiveExpiry`.
- **Job unit (InMemory `TestApplicationDbContext` + fake `IItemClassifier`):** cache-skip when version
  current; insert when missing; re-classify + update when version stale; drop (no write) on
  `Result.Fail`; empty normalized name → no-op. (The unique-race `DbUpdateException` catch is defensive
  — InMemory doesn't enforce unique indexes; covered by code review + the integration path, noted here
  so it isn't mistaken for untested behavior.)
- **Trigger unit (FakeItEasy):** `Null` trigger does **not** enqueue (assert queue not called);
  `Queueing` trigger enqueues (assert `TryEnqueue` called).
- **Integration (`Frigorino.IntegrationTests`, 1 happy path):** register a deterministic **stub**
  `IItemClassifier` + `Classifier:Enabled=true` in the test host; add a list item; **poll** for the
  `Product` row (async fire-and-forget — poll with a timeout, matching existing async assertions) and
  assert the expected classification. Proves slice → trigger → queue → job → DB end-to-end on real
  Postgres.
- **Final gate:** full `dotnet test Application/Frigorino.sln` (Test + IntegrationTests) + `docker
  build`. No frontend changes, so no lint/tsc/prettier/`npm run api`.

## Rollout / ops

- Lossy by design: work queued-but-not-run is lost on restart/deploy/sleep; re-triggered (cheaply,
  cache-aware) on the next reference of the same name.
- Cost is rounding error at this volume (~$1–10/year). Disabled by default until `Classifier:*` is set.
- Reversible: disabling the flag stops all classification; the `Product` table is derived data,
  cascade-deleted with households.
```
