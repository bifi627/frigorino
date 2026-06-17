# replay — design spec

**Date:** 2026-06-17
**Status:** design approved; pending implementation plan
**Type:** agent-tooling skill (sibling to `dreamer`)

## Summary

`replay` mines past Claude Code session transcripts to curate the agent's file-based
memory store. It extracts candidate memories from transcripts, reconciles them against
the live store, triages each change by confidence (auto-apply / ask-the-user / drop),
stages everything non-destructively, walks the user through the uncertain items, applies
on approval, and hands off to `dreamer` for final verification.

It is the **generative half** that `dreamer` lacks. `dreamer` curates what is *already
written* (reconcile index↔files, prune stale, normalize identifiers, verify links) using
the store + the live repo. `replay` surfaces what was *never written down* — durable
preferences, corrections, and decisions that recurred across sessions but no one ran
`Write` on — plus transcript-driven upkeep of existing memories that `dreamer` can't see
(it reads code/git, not transcripts).

Local, subscription-billed equivalent of Anthropic's Managed-Agents "Dreams" feature
(captured in `IDEAS.md`).

## Relationship to `dreamer`

Separate skills, by deliberate decision (not folded into `dreamer`):

- **Different write-discipline.** `dreamer` is *verify → edit-in-place* (safe cleanup,
  scan-verifiable, no gate). `replay` is *propose → diff → ask → apply* (probabilistic,
  non-destructive, human-in-loop). Opposite safety models.
- **Different trigger & cadence.** `dreamer` runs reactively and cheap; `replay` runs
  deliberately and spends a subscription window.
- **Different trust boundary.** `dreamer` reads only the store + repo (trusted). `replay`
  ingests untrusted transcripts (poisoning/fabrication risk lives there).
- **No re-test of the committed `dreamer`.** Keeping `replay` separate leaves the
  already-built, already-tested `dreamer` untouched.

Dependency direction: `replay` runs, then recommends `dreamer` as the post-pass.
`dreamer`'s scan-based verification block is `replay`'s final safety net.

## Locked decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Transcript scoping | **Incremental (watermark)** | Persist last-replayed marker; each run only mines new sessions. Gives the time signal for free (transcript `.timestamp`), avoiding the poisoned-mtime trap. |
| v1 product | **Reconcile + discover** | Both transcript-driven upkeep of existing memories AND net-new insight discovery. Human-in-the-loop triage makes the riskier discover half safe. |
| Apply boundary | **Guided apply, `dreamer` verifies** | `replay` owns mine→stage→triage→guided-apply; `dreamer` is the post-verify. |
| Extraction execution | **Parallel subagent map** | One subagent per transcript (flat fan-out, no nesting). Reduce/triage/apply stay in-session so the guided Q&A works. |
| Execution tier | **In-session skill (tier a)** | Forced by guided interactive apply — a background Workflow can't run the apply Q&A. The extraction map still fans out subagents. |
| Memory VCS | **Out of scope** | `replay` is git-agnostic. Non-destructive safety = staging + pre-apply backup. Git memory-tracking is a separate, manual environment setup (delivered as a hand-off, not baked into the skill). |

## Architecture — five phases

```
replay (in-session):
  1. SELECT     transcripts newer than the watermark (bounded by the batch guard)
  2. EXTRACT    parallel map: one subagent per reduced transcript → candidate memories
  3. RECONCILE  in-session: match each candidate vs the live store → ADD/UPDATE/DELETE/NOOP
  4. TRIAGE     tag each op: auto-confident | ask | drop
  5. APPLY      stage → summary diff + batched Q&A → backup → write live → advance watermark
                                    │
dreamer (existing, unchanged): ─────┘ post-pass: normalize ids + verify links + reconcile vs repo
```

### Phase 1 — SELECT + pre-reduction

- **Locate transcripts for free.** The `*.jsonl` files are siblings of `memory/` inside
  `~/.claude/projects/<project-slug>/`. `replay` finds them relative to the memory dir —
  no hardcoded path, automatically scoped to this project (no cross-project leakage).
- **Watermark state** — `memory/.replay-state.json` (dotfile; `dreamer`'s `*.md` scans
  ignore it). Stores processed `sessionId`s + last-run timestamp. SELECT = sessions not in
  the set, **excluding the currently-active session** (incomplete; it's the one `replay`
  runs in).
- **Pre-reduction** — a deterministic script (`reduce_transcript.py`; python is present,
  jq is not) collapses each transcript to a compact dialogue skeleton before any subagent
  sees it:

  | Keep | Drop |
  |------|------|
  | `user` turns (string content — corrections/preferences) | `tool_use` inputs + `toolUseResult` bodies (the MB of file dumps/output) |
  | `assistant` **text** blocks (stated decisions) | `assistant` **thinking** blocks (huge, internal, noisy) |
  | `timestamp`, `sessionId`, `gitBranch` (provenance) | metadata rows (`mode`, `attachment`, `file-history-snapshot`, hooks, `ai-title`, …) |

  Turns a 14 MB transcript into a few KB. Shipped as a supporting file in the skill dir.
- **Thinking blocks dropped by default** (size + noise). Accepted v1 risk: an insight the
  agent only *thought* but never *said* is lost; visible dialogue carries the durable signal.

### Phase 2 — EXTRACT (parallel map)

Each map subagent reads one reduced transcript + the `MEMORY.md` index (~73 one-liners, so
it skips re-proposing the obviously-known) and returns **structured candidates**, not prose:

```
{ proposed_type: feedback | user | project | reference,
  signal: explicit | implicit,    # explicit = stated/corrected/approved; implicit = consistent behavioral pattern
  proposed_name: <kebab slug>,    description: <one-line>,
  body: <the fact, in the store's voice>,
  evidence: <verbatim quote (explicit) OR representative cited instances + count (implicit)>,
  session_id, timestamp,          # provenance
  recurrence: <# distinct turns it showed up in (within this transcript)>,
  extractor_confidence: low | med | high }   # "durable, or one-off?"
```

**What counts as a candidate — biased to `replay`'s sweet spot:**

| Type | Priority | Signal |
|------|----------|--------|
| **feedback** | primary | **Technical preferences** — naming conventions, tool/library/command choices, code formatting & style, workflow/verification habits — plus corrections ("no, use X not Y"), stated rules ("always/never/from now on"), and confirmed approaches ("yes, do it that way"). The densest vein in coding transcripts and the bulk of the existing store. |
| **user** | high | Who they are — role, expertise, durable preferences. |
| **project** | conservative | Only constraints/decisions **not derivable from code or git**. Most project facts are repo-derivable → `dreamer`'s job to prune. |
| **reference** | opportunistic | A URL/dashboard/ticket repeatedly returned to. Rare from transcripts. |

**Hard noise filter (ignore):** one-off debugging facts, ephemeral state, task-specific
detail, anything repo/git-derivable, anything the index already covers (unless the
transcript *contradicts* it — that's a real signal).

**Grounding is mandatory.** Every candidate carries a verbatim quote + session_id +
timestamp. A candidate with no quotable evidence is dropped. This is the antidote to the
compounding-error/hallucination risk: at apply time the user verifies evidence, not a summary.
This also draws the explicit/implicit line for technical preferences. **Explicit** preferences
(stated convention, correction, or approval) ground on a statement quote and can be auto-staged.
**Implicit** preferences — a consistent *choice among real alternatives* the agent made without
objection (block braces, Bash over PowerShell, kebab-case) — have no statement, so they ground
on a **behavioral pattern**: representative cited instances + a recurrence count. Implicit
candidates are surfaced only when that pattern is *strong* (a consistent choice among
alternatives, not arbitrary tool use) and **always route to `ask`, never auto** — you confirm
before an unspoken habit becomes a written rule.

**Division of labor:** map agents stay parallel and cheap — they see only the index, not
the full store; dedup/matching against actual files is RECONCILE's job. Model: **Sonnet**
(highest volume; bounded, schema'd judgment).

### Phase 3 — RECONCILE (in-session)

The orchestrator (session model, Opus) takes all candidates + the full store (73 files →
matches against the whole thing, no embeddings) and assigns one verdict per candidate:

- **ADD** — no existing match → net-new memory.
- **UPDATE** — matches an existing memory but changes/refines it (new value, or a
  contradiction reflecting a newer truth).
- **DELETE** — transcript evidence shows an existing memory is now wrong (e.g. a reversed
  preference). Rare from transcripts.
- **NOOP** — already captured accurately → drop.

**Cross-transcript aggregation happens here:** the same fact from 3 sessions collapses into
one op with combined `recurrence: 3`, all evidence quotes, and the timestamp span.
**Recurrence across sessions is the strongest durability signal** (frequency ≈ importance);
"said once in three separate sessions" outranks "said three times in one."

### Phase 4 — TRIAGE

Tag each op, by type × confidence:

| Op | Auto-stage (shown in summary) | Ask |
|----|------|-----|
| **NOOP** | — (dropped silently) | — |
| **ADD** | high: recurred ≥2 sessions, strong quote, durable, `feedback`/`user` | low/med: seen once, borderline durable-vs-one-off, or a `project` candidate maybe repo-derivable |
| **UPDATE** | (configurable) only an obvious, unambiguous refinement | **default** — alters curated knowledge |
| **DELETE** | never | **always** — most destructive op |
| **any op, implicit signal** | never | **always** — gated: surfaced only if consistent (≥2 cited instances, within or across sessions; cross-session is stronger), zero contradicting corrections; else dropped. (Cross-session-only is too brittle — extraction is stochastic.) |

**The six ask-triggers:** (1) contradiction with no clear current value, (2)
durable-vs-one-off uncertainty, (3) any DELETE / destructive UPDATE, (4) a merge that would
collapse a real distinction, (5) a `project` candidate possibly already in the repo, (6) an
implicit (behavioral-pattern) preference — always asked, never auto.

**Guardrail:** ask *only* when the answer changes what gets written **and** can't be settled
from the quoted evidence. If the quote resolves it, auto-stage — don't perform a question.
Asks are collected, not fired mid-reconcile.

### Phase 5 — APPLY (guided, git-agnostic)

1. **Stage** the proposed end-state into `memory.dream/` — a parallel copy of `memory/`
   with approved-pending ops applied (mirrors Dreams' separate output store). Ephemeral.
2. **Review.** Auto-staged ops → a summary digest (`N adds · M updates · K deletes`, one
   line each, evidence one keystroke away). "Ask" ops → batched questions (≤4 per prompt),
   each showing candidate + verbatim evidence + the existing memory it touches → user picks
   keep / edit / reject.
3. **Apply** (after approval), in order:
   a. Auto-snapshot `memory/` → timestamped backup (git-agnostic revert path).
   b. Apply approved ops to live `memory/` (write/edit/delete).
   c. Update `MEMORY.md` for adds/deletes (index and files move together — `dreamer`'s rule).
   d. Advance the watermark in `.replay-state.json`.
   e. Clear `memory.dream/`.
4. **Hand-off.** Recommend a `dreamer` run on the freshly-applied store.

**Safety stack without git:** staging (never writes live unreviewed) → batched human
adjudication of risky ops → pre-apply backup → `dreamer` verification pass. Four layers,
none needing VCS.

## Models

| Phase | Model | Why |
|-------|-------|-----|
| Pre-reduction | none (python script) | Deterministic — free. |
| EXTRACT (map) | **Sonnet** | Highest volume; bounded schema'd judgment. Not below Sonnet — signal-vs-noise discrimination is the skill's entire value. Knob: bump to Opus for a deliberate thorough run. |
| RECONCILE / TRIAGE / APPLY | **session model (Opus)** | Lowest volume, highest stakes (a wrong UPDATE/DELETE corrupts curated memory); needs the whole store in context. |

## Batch guard

After SELECT, before spending anything: count pending sessions + estimate reduced size.

- **≤ threshold** (default **10** pending sessions) → proceed silently.
- **> threshold** → stop and ask: `Found X pending sessions (~Y KB, ~Z tokens). →
  [a] mine all (in waves) · [b] most-recent N · [c] date cutoff · [d] cancel`.

First run (empty watermark → all pending) is just the common case of this guard, not
special-cased. The fan-out runs in waves (concurrency caps ~10–16), so "mine all" is
mechanically fine even at 158; the guard is about letting the user decide window spend.
Threshold configurable.

## Provenance + closing report

- **Provenance.** The store already uses `metadata.originSessionId` on some memories.
  `replay` follows it: every applied memory is stamped with the `originSessionId` it was
  extracted from (the strongest source, for aggregated ones), so any entry is traceable to
  its evidence. No separate confidence field — everything applied was auto-confident or
  human-confirmed, so "applied" means "trusted." `dreamer` leaves these keys alone.
- **Closing report (the upstream loop).** `replay` ends with a one-screen summary:
  `X applied (A adds / U updates / D deletes), Y asked, Z dropped as noise`. If several "ask"
  items were first-seen-this-batch preferences, it notes them — a nudge that the *save-time*
  discipline is missing that category. Just the last line of output; no new machinery.

## Out of scope (v1)

- **Git memory-tracking** — delivered as a separate, manual environment-setup hand-off, not
  built into the skill. The skill stays git-agnostic.
- **Thinking-block mining** — dropped at pre-reduction.
- **Workflow tier-b** (full map-reduce orchestration script) — only if transcript volume
  outgrows in-session fan-out. The guided apply keeps the orchestration in-session for now.
- **Cross-project mining** — scoped to this project's transcript dir by construction.

## Testing plan

`replay` is a *technique* skill → tested by application (writing-skills TDD; the
RED→GREEN→REFACTOR runs during implementation, per the Iron Law).

- **Sandbox always.** Fixture `.jsonl` transcripts + a throwaway copy of the store in a temp
  dir. Never the live memory. (Same discipline used to build `dreamer`.)
- **RED baseline** — a subagent does "mine these transcripts and curate memory" *without*
  the skill. Expected failures to document: reads raw transcripts → context blowup;
  over-extracts one-off noise; no evidence grounding; mutates the live store directly; no
  triage/asking.
- **GREEN** — write the skill; agent follows the five phases, stages, grounds, triages, asks.
- **Scenarios (≥2, incl. a boundary):**
  1. *Application:* fixtures with a durable preference recurring across 2 sessions + a one-off
     debug fact + a contradiction of an existing memory → expect: preference **auto-added**,
     one-off **dropped**, contradiction **asked**.
  2. *Boundary:* 0 new sessions (watermark current) → graceful no-op; and a > threshold batch
     → the guard stops and asks.
- **REFACTOR** — close loopholes the runs expose; re-test before claiming done.

## Open questions / future

- Tier-b Workflow promotion if/when in-session fan-out can't hold the batch.
- How aggressive `discover` should be (Reflexion-style meta-patterns) once v1 quality is seen.
- Whether the closing-report "save-time gaps" nudge should eventually feed a SessionStart hook.
