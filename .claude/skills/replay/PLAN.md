# replay Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `replay` skill — mines past Claude Code session transcripts to surface and reconcile memories never written down, with a guided human-in-the-loop apply.

**Architecture:** A deterministic python reducer (`reduce_transcript.py`) collapses raw JSONL transcripts to compact dialogue skeletons; `SKILL.md` then instructs the agent through five phases (SELECT → EXTRACT via parallel Sonnet subagents → RECONCILE/TRIAGE in-session on Opus → guided APPLY → hand off to `dreamer`). The reducer is unit-tested; the skill is validated with writing-skills subagent scenarios against sandbox fixtures.

**Tech Stack:** Python 3 (stdlib only — `json`, `unittest`; **no pytest**, **no jq** in this environment). Markdown for the skill. The skill itself shells out only to the reducer and to file ops.

**Authoritative spec:** `.claude/skills/replay/DESIGN.md` (committed `df84b47`). This plan implements it; on any conflict the spec wins.

## Global Constraints

- **Python stdlib only.** Tests use `unittest` (run `python -m unittest`), never pytest. `jq` is absent — never use it.
- **Sandbox, never live.** All scenarios/tests run in `C:\tmp\replay-sandbox\` (copy fixtures there). NEVER point a test or scenario at the live store `~/.claude/projects/C--Repositories-frigorino/memory`. (`C:\tmp` is used explicitly because the Write tool and Bash resolve `/tmp` differently on Windows.)
- **Skill is git-agnostic.** `SKILL.md` issues no `git` commands. Safety = staging + pre-apply timestamped backup.
- **Models at execution time:** EXTRACT subagents use **Sonnet** and must be **flat** (they may NOT spawn their own subagents); RECONCILE/TRIAGE/APPLY run in-session on the session model (**Opus**).
- **writing-skills Iron Law:** the RED baseline (Task 3) must run and be observed BEFORE `SKILL.md` is written (Task 4). After any REFACTOR edit to `SKILL.md`, re-run ≥2 scenarios incl. the boundary case before claiming done.
- **Commits:** conventional style (`feat(skills):`, `chore(skills):`, `docs(skills):`), one per task. No `Co-Authored-By` or "Generated with Claude" trailers. Work on branch `stage`, local commits only — the user pushes.
- **No nested subagents** anywhere (flat orchestrator → workers).

---

## File Structure

- `.claude/skills/replay/SKILL.md` — the skill (Task 4). Author per `DESIGN.md`.
- `.claude/skills/replay/reduce_transcript.py` — deterministic pre-reduction script (Tasks 1–2).
- `.claude/skills/replay/test_reduce_transcript.py` — unittest suite for the reducer (Tasks 1–2).
- `.claude/skills/replay/fixtures/transcripts/*.jsonl` — synthetic transcripts exercising the scenarios (Task 3).
- `.claude/skills/replay/fixtures/memory/` — throwaway seed memory store for scenarios (Task 3).
- `.claude/skills/replay/fixtures/README.md` — what each fixture exercises + the expected `replay` behavior (the test oracle) (Task 3).
- `.claude/skills/replay/DESIGN.md` — exists, committed. Not modified by this plan.
- `IDEAS.md` — the `local-memory-dreams` entry is updated at the end (Task 6).

---

### Task 1: Reducer core — `reduce_records`

The pure, in-memory mapping from raw JSONL records to compact dialogue turns. No file I/O yet.

**Files:**
- Create: `.claude/skills/replay/reduce_transcript.py`
- Test: `.claude/skills/replay/test_reduce_transcript.py`

**Interfaces:**
- Produces: `reduce_records(records: list[dict]) -> list[dict]`. Each output item is `{"role": "user"|"assistant", "text": str, "timestamp": str|None, "session_id": str|None, "git_branch": str|None}`. Keeps real user turns (string/text content) and assistant `text` blocks; drops assistant `thinking`/`tool_use` blocks, tool-result echoes, sidechains, and all metadata record types; drops any turn whose extracted text is empty.

- [ ] **Step 1: Write the failing test**

Create `.claude/skills/replay/test_reduce_transcript.py`:

```python
import unittest
from reduce_transcript import reduce_records


class TestReduceRecords(unittest.TestCase):
    def test_keeps_dialogue_drops_noise(self):
        records = [
            {"type": "user", "timestamp": "2026-06-10T10:00:00Z", "sessionId": "s1",
             "gitBranch": "stage",
             "message": {"role": "user", "content": "always use block braces"}},
            {"type": "assistant", "timestamp": "2026-06-10T10:00:05Z", "sessionId": "s1",
             "message": {"role": "assistant", "content": [
                 {"type": "thinking", "thinking": "user wants braces"},
                 {"type": "text", "text": "Got it, I'll use block braces."},
                 {"type": "tool_use", "name": "Edit", "input": {"x": 1}}]}},
            # tool-result echo recorded as a 'user' record -> dropped
            {"type": "user", "timestamp": "2026-06-10T10:00:10Z", "sessionId": "s1",
             "toolUseResult": {"stdout": "..."},
             "message": {"role": "user",
                         "content": [{"type": "tool_result", "content": "big output"}]}},
            {"type": "system", "subtype": "hook", "content": None},
            {"type": "file-history-snapshot", "snapshot": {}},
            {"type": "ai-title"},
            # sidechain (subagent) noise -> dropped
            {"type": "assistant", "isSidechain": True, "sessionId": "s1",
             "message": {"role": "assistant",
                         "content": [{"type": "text", "text": "sidechain noise"}]}},
            # assistant with only tool_use -> empty text -> dropped
            {"type": "assistant", "sessionId": "s1",
             "message": {"role": "assistant",
                         "content": [{"type": "tool_use", "name": "Read", "input": {}}]}},
        ]
        self.assertEqual(reduce_records(records), [
            {"role": "user", "text": "always use block braces",
             "timestamp": "2026-06-10T10:00:00Z", "session_id": "s1", "git_branch": "stage"},
            {"role": "assistant", "text": "Got it, I'll use block braces.",
             "timestamp": "2026-06-10T10:00:05Z", "session_id": "s1", "git_branch": None},
        ])


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd .claude/skills/replay && python -m unittest test_reduce_transcript -v`
Expected: FAIL — `ImportError`/`ModuleNotFoundError` (no `reduce_transcript.py`) or `cannot import name 'reduce_records'`.

- [ ] **Step 3: Write minimal implementation**

Create `.claude/skills/replay/reduce_transcript.py`:

```python
"""Collapse a Claude Code JSONL session transcript to a compact dialogue skeleton.

Keeps the human <-> main-agent dialogue (user turns + assistant text); drops the
tool I/O, thinking, sidechains, and metadata that make raw transcripts huge.
Used by the `replay` skill before any extraction subagent reads a transcript.
"""
import json
import sys

_DROP_TYPES = {
    "system", "file-history-snapshot", "attachment", "mode",
    "permission-mode", "ai-title", "last-prompt",
}


def _assistant_text(content):
    if isinstance(content, str):
        return content.strip()
    if isinstance(content, list):
        parts = [b.get("text", "") for b in content
                 if isinstance(b, dict) and b.get("type") == "text"]
        return "\n".join(p for p in parts if p).strip()
    return ""


def _user_text(content):
    if isinstance(content, str):
        return content.strip()
    if isinstance(content, list):
        # a tool_result block means this 'user' record is a tool echo, not prose
        if any(isinstance(b, dict) and b.get("type") == "tool_result" for b in content):
            return ""
        parts = [b.get("text", "") for b in content
                 if isinstance(b, dict) and b.get("type") == "text"]
        return "\n".join(p for p in parts if p).strip()
    return ""


def reduce_records(records):
    out = []
    for r in records:
        if not isinstance(r, dict):
            continue
        if r.get("isSidechain"):
            continue
        t = r.get("type")
        if t in _DROP_TYPES:
            continue
        msg = r.get("message") or {}
        if t == "user":
            if "toolUseResult" in r:
                continue
            text, role = _user_text(msg.get("content")), "user"
        elif t == "assistant":
            text, role = _assistant_text(msg.get("content")), "assistant"
        else:
            continue
        if not text:
            continue
        out.append({
            "role": role,
            "text": text,
            "timestamp": r.get("timestamp"),
            "session_id": r.get("sessionId"),
            "git_branch": r.get("gitBranch"),
        })
    return out
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd .claude/skills/replay && python -m unittest test_reduce_transcript -v`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/replay/reduce_transcript.py .claude/skills/replay/test_reduce_transcript.py
git commit -m "feat(skills): add replay transcript reducer core"
```

---

### Task 2: Reducer file I/O + CLI — `reduce_file` and `__main__`

Read a real `.jsonl` file resiliently (skip blank/malformed lines), and expose a CLI the skill calls.

**Files:**
- Modify: `.claude/skills/replay/reduce_transcript.py`
- Test: `.claude/skills/replay/test_reduce_transcript.py`

**Interfaces:**
- Consumes: `reduce_records` (Task 1).
- Produces: `reduce_file(path: str) -> list[dict]` (skips blank lines and lines that fail `json.loads`); CLI `python reduce_transcript.py <transcript.jsonl>` prints the reduced turns as a JSON array (indent=2, `ensure_ascii=False`) to stdout, exit 2 on bad args.

- [ ] **Step 1: Write the failing tests**

Append to `.claude/skills/replay/test_reduce_transcript.py` (add the import at top: `import json, os, tempfile`):

```python
class TestReduceFile(unittest.TestCase):
    def _write(self, text):
        fd, path = tempfile.mkstemp(suffix=".jsonl")
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            f.write(text)
        self.addCleanup(os.remove, path)
        return path

    def test_skips_blank_and_malformed_lines(self):
        path = self._write(
            '{"type":"user","sessionId":"s1","message":{"role":"user","content":"hi"}}\n'
            "\n"
            "{ this is not json }\n"
            '{"type":"assistant","sessionId":"s1","message":{"role":"assistant",'
            '"content":[{"type":"text","text":"hello"}]}}\n'
        )
        result = reduce_file(path)
        self.assertEqual([r["text"] for r in result], ["hi", "hello"])

    def test_empty_file_returns_empty_list(self):
        self.assertEqual(reduce_file(self._write("")), [])
```

Add `from reduce_transcript import reduce_records, reduce_file` (replace the existing import line).

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd .claude/skills/replay && python -m unittest test_reduce_transcript -v`
Expected: FAIL — `cannot import name 'reduce_file'`.

- [ ] **Step 3: Add `reduce_file` + CLI**

Append to `.claude/skills/replay/reduce_transcript.py`:

```python
def reduce_file(path):
    records = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                records.append(json.loads(line))
            except ValueError:
                continue
    return reduce_records(records)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("usage: python reduce_transcript.py <transcript.jsonl>", file=sys.stderr)
        sys.exit(2)
    print(json.dumps(reduce_file(sys.argv[1]), ensure_ascii=False, indent=2))
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd .claude/skills/replay && python -m unittest test_reduce_transcript -v`
Expected: PASS (3 tests).

- [ ] **Step 5: CLI smoke test against a real transcript (read-only)**

Run: `python .claude/skills/replay/reduce_transcript.py "$HOME/.claude/projects/C--Repositories-frigorino/a7cd6d72-2cec-414d-8276-6c1e3211ff9c.jsonl" | head -40`
Expected: a JSON array of `{role,text,...}` turns; dramatically smaller than the raw file; no `thinking`/tool payloads. (Read-only; mutates nothing.)

- [ ] **Step 6: Commit**

```bash
git add .claude/skills/replay/reduce_transcript.py .claude/skills/replay/test_reduce_transcript.py
git commit -m "feat(skills): add replay reducer file reader and CLI"
```

---

### Task 3: Fixtures + RED baseline

Build the synthetic test world, then watch an agent fail the task WITHOUT the skill (writing-skills RED). The observed failures are the checklist `SKILL.md` must defeat in Task 4.

**Files:**
- Create: `.claude/skills/replay/fixtures/transcripts/sess-A.jsonl`, `sess-B.jsonl`, `sess-C.jsonl`
- Create: `.claude/skills/replay/fixtures/memory/MEMORY.md` + 2 seed memory files
- Create: `.claude/skills/replay/fixtures/README.md`

- [ ] **Step 1: Create the seed memory store**

`.claude/skills/replay/fixtures/memory/MEMORY.md`:

```markdown
- [Deploy target](project_deploy_target.md) — where the app is hosted
- [Test runner preference](feedback_test_runner.md) — how to run tests
```

`.claude/skills/replay/fixtures/memory/project_deploy_target.md`:

```markdown
---
name: project_deploy_target
description: Where the app is hosted
metadata:
  type: project
---

The app deploys to Heroku.
```

`.claude/skills/replay/fixtures/memory/feedback_test_runner.md`:

```markdown
---
name: feedback_test_runner
description: How to run the test suite
metadata:
  type: feedback
---

Run tests with `make test`.
```

- [ ] **Step 2: Create the fixture transcripts**

Each `.jsonl` is a small set of records (user/assistant), with `sessionId` and ascending `timestamp`s. Author them to exercise, in total:
- **Durable explicit preference, recurring across 2 sessions** — in `sess-A` and `sess-B` the user says some form of "always run `npm run lint` before committing". Expect: AUTO-add a `feedback` memory (recurrence ≥2 sessions).
- **One-off debug fact** — in `sess-A` the user says "port 5001 is busy today, use 5002". Expect: DROPPED as noise.
- **Contradiction of a seed memory** — in `sess-C` the user says "we moved off Heroku, we're on Railway now". Expect: ASK (UPDATE/contradiction of `project_deploy_target`).
- **Implicit preference (strong, never stated)** — across `sess-B` and `sess-C` the assistant consistently writes block-brace style and is never corrected. Expect: ASK (implicit signal, gated on cross-session consistency), never auto.

Use this record shape (one JSON object per line):
```json
{"type":"user","timestamp":"2026-06-11T09:00:00Z","sessionId":"sess-A","gitBranch":"stage","message":{"role":"user","content":"always run npm run lint before committing"}}
{"type":"assistant","timestamp":"2026-06-11T09:00:03Z","sessionId":"sess-A","message":{"role":"assistant","content":[{"type":"text","text":"Understood — I'll run npm run lint first."}]}}
```

- [ ] **Step 3: Write the fixtures README (the test oracle)**

`.claude/skills/replay/fixtures/README.md` lists each fixture signal and the expected `replay` verdict (auto-add / drop / ask), so scenarios in Tasks 4–5 have an unambiguous pass/fail oracle. Include the four bullets above verbatim with their expected verdicts.

- [ ] **Step 4: Set up the sandbox and run the RED baseline**

```bash
rm -rf /c/tmp/replay-sandbox && mkdir -p /c/tmp/replay-sandbox
cp -r .claude/skills/replay/fixtures/memory /c/tmp/replay-sandbox/memory
cp -r .claude/skills/replay/fixtures/transcripts /c/tmp/replay-sandbox/transcripts
```

Dispatch ONE general-purpose subagent (no `replay` skill available to it) with this task:
> "Transcripts are at `C:\tmp\replay-sandbox\transcripts\*.jsonl`; a memory store is at `C:\tmp\replay-sandbox\memory\` (markdown + MEMORY.md index). Mine the transcripts and update the memory store with anything worth remembering long-term."

- [ ] **Step 5: Record the baseline failures**

Observe and write down (in the implementation session — these feed Task 4) which of these the unguided agent does wrong. Expected failures per the spec: reads raw transcripts (context blowup / no use of a reducer); over-extracts the one-off "port busy" fact; invents memories without quotable evidence; writes the contradiction silently instead of asking; mutates the store with no staging/backup; no implicit-preference handling.

- [ ] **Step 6: Commit the fixtures**

```bash
git add .claude/skills/replay/fixtures
git commit -m "test(skills): add replay sandbox fixtures and test oracle"
```

(The baseline run mutated only `C:\tmp\replay-sandbox`, which is disposable — nothing to commit from it.)

---

### Task 4: Write `SKILL.md` (GREEN)

Author the skill so a guided agent does what the unguided one (Task 3) got wrong. Then re-run the application scenario WITH the skill and confirm compliance.

**Files:**
- Create: `.claude/skills/replay/SKILL.md`

- [ ] **Step 1: Write the frontmatter (verbatim)**

```markdown
---
name: replay
description: Use when mining past Claude Code session transcripts to surface and reconcile memories the agent never wrote down — recurring preferences, corrections, naming/tooling/formatting conventions, or decisions that span sessions. Triggered by "replay my sessions", "what did I forget to remember", "mine the transcripts", or /replay. Complements dreamer (which curates already-written memory; replay reads the JSONL transcripts). Not for writing one memory by hand or tidying the existing store — use dreamer for that.
---
```

(CSO: triggers/symptoms only, no workflow summary. Cross-references `dreamer` by name.)

- [ ] **Step 2: Write the body**

Author the body from `DESIGN.md` (the authoritative content). Structure: Overview → When to use / not → The five phases → Models → Candidate schema → Extraction criteria + grounding → Reconcile → Triage + ask-triggers → Apply → Hand-off → Verification checklist → Common mistakes. Keep it scannable; mirror `dreamer`'s tone.

**Non-negotiables the body MUST state explicitly** (each is a baseline failure from Task 3):
1. NEVER read raw transcripts into context — always run `python reduce_transcript.py <path>` first and work from the reduced output.
2. EXTRACT fans out one **Sonnet** subagent per reduced transcript; subagents are **flat** (must not spawn their own subagents) and return the structured candidate schema, not prose.
3. Every candidate MUST carry quotable evidence — a statement quote (explicit) or cited recurring instances + count (implicit). No evidence → drop.
4. **Implicit** preferences ALWAYS route to `ask`, never auto, and only surface when strong (consistent across ≥2 sessions, zero contradicting corrections).
5. NEVER write to live `memory/` unreviewed: stage to `memory.dream/`, show the summary diff, run the batched ≤4-item Q&A for the `ask` items, and ALWAYS snapshot a timestamped backup of `memory/` immediately before applying.
6. Exclude the currently-active session from SELECT.
7. On apply: write files, update `MEMORY.md` (index + files move together), stamp `metadata.originSessionId`, advance the watermark in `.replay-state.json`, clear `memory.dream/`.
8. Finish by recommending a `dreamer` run; the skill issues NO `git` commands.

- [ ] **Step 3: Re-run the application scenario WITH the skill**

```bash
rm -rf /c/tmp/replay-sandbox && mkdir -p /c/tmp/replay-sandbox
cp -r .claude/skills/replay/fixtures/memory /c/tmp/replay-sandbox/memory
cp -r .claude/skills/replay/fixtures/transcripts /c/tmp/replay-sandbox/transcripts
```

Run `replay` (in-session) pointed at the sandbox (override the transcript dir + memory dir to the sandbox copies). Adjudicate the Q&A as a normal user would: confirm the Railway contradiction, confirm the implicit block-brace preference.

- [ ] **Step 4: Verify against the oracle**

Check the run against `fixtures/README.md`:
- `npm run lint` preference → **auto-added** as a `feedback` memory with `originSessionId`.
- "port busy" → **dropped** (not in the store, not in staging).
- Heroku→Railway → **asked**, then applied as an UPDATE/replace.
- block-brace implicit preference → **asked** (not auto), applied only on confirmation.
- A pre-apply backup of the sandbox `memory/` exists; `MEMORY.md` reconciles with the files; watermark advanced.

Expected: all five hold. If any fails, that's a GREEN miss — fix `SKILL.md` and re-run before committing.

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/replay/SKILL.md
git commit -m "feat(skills): add replay skill (transcript-mining memory curation)"
```

---

### Task 5: Boundary scenario + REFACTOR

Close loopholes the application scenario can't reach: the empty batch and the large-batch guard.

**Files:**
- Modify: `.claude/skills/replay/SKILL.md` (only if a loophole is found)

- [ ] **Step 1: Empty-batch scenario**

Set the sandbox watermark to mark all fixture sessions already processed (write `C:\tmp\replay-sandbox\memory\.replay-state.json` listing `sess-A/B/C`). Run `replay`.
Expected: graceful **no-op** — reports "no new sessions", spends no extraction subagents, makes no writes.

- [ ] **Step 2: Batch-guard scenario**

Clear the watermark and set the guard threshold low (e.g. 2). Run `replay`.
Expected: it **stops and asks** before extracting (`Found 3 pending sessions … [a] all [b] recent N [c] cutoff [d] cancel`) rather than silently mining everything.

- [ ] **Step 3: REFACTOR for any loophole**

If either scenario misbehaves (e.g. the skill mines despite a current watermark, or skips the guard), fix `SKILL.md` to close it explicitly.

- [ ] **Step 4: Re-run BOTH the boundary scenarios AND the Task 4 application scenario**

Per the Iron Law, a REFACTOR edit invalidates prior green. Re-run all three (application + empty + guard) and confirm each still passes its oracle. If no `SKILL.md` edit was needed in Step 3, note that and still re-confirm the boundary scenarios.

- [ ] **Step 5: Commit (only if SKILL.md changed)**

```bash
git add .claude/skills/replay/SKILL.md
git commit -m "fix(skills): close replay boundary-case loopholes"
```

---

### Task 6: Holistic review, discoverability, IDEAS.md

**Files:**
- Modify: `IDEAS.md`

- [ ] **Step 1: Holistic review on Opus**

Dispatch a final read-only review subagent **on Opus** (an Explore agent) to read `DESIGN.md` + `SKILL.md` + `reduce_transcript.py` together and check: spec coverage (all five phases + the six ask-triggers + implicit handling present), the CSO description doesn't leak workflow, the non-negotiables from Task 4 Step 2 are all stated, and `SKILL.md` issues no `git`. Fix anything it flags, then re-confirm the application scenario if `SKILL.md` changed.

- [ ] **Step 2: Discoverability check**

Confirm `replay` appears in the available-skills list with its description (skills under `.claude/skills/` are auto-discovered). Confirm `/replay` resolves.

- [ ] **Step 3: Update IDEAS.md**

In the `local-memory-dreams` entry, mark the `replay` skill as implemented (tier-a shipped): trim the "Effort tiers (a)" line to a "Done — see `.claude/skills/replay/`" note, and leave tiers (b) cron/Workflow and (c) scheduling as remaining future work. Do not delete the whole entry (multi-cycle item — only the shipped cycle is done).

- [ ] **Step 4: Final verification**

Run the full reducer suite once more: `cd .claude/skills/replay && python -m unittest test_reduce_transcript -v` → all pass.

- [ ] **Step 5: Commit**

```bash
git add IDEAS.md
git commit -m "docs(ideas): mark replay (tier-a local-memory-dreams) shipped"
```

---

## Post-plan (not a task)

Owed separately to the user (out of scope for the skill, per the spec): a write-up on setting up **git memory-tracking** for `~/.claude/projects/C--Repositories-frigorino/memory/` in their environment.

---

## Self-Review

**Spec coverage:** SELECT/pre-reduce → Tasks 1–2 (reducer) + Task 4 phase prose; EXTRACT (Sonnet map, flat) → Task 4 non-negotiables #1–#3; RECONCILE (ADD/UPDATE/DELETE/NOOP) → Task 4 body + Task 4 Step 4 oracle; TRIAGE (auto/ask/drop + implicit + six triggers) → Task 4 #3–#4 + fixtures exercising auto/drop/ask + implicit; APPLY (stage/backup/Q&A/watermark/MEMORY.md/handoff) → Task 4 #5–#8 + Task 4 Step 4; batch guard + watermark → Task 5; provenance `originSessionId` → Task 4 #7 + oracle; models → Global Constraints + Task 4 #2; git-agnostic → Global Constraints + Task 4 #8; testing discipline → Tasks 3–5. No uncovered spec section found.

**Placeholder scan:** reducer code and tests are complete and runnable. The one intentional authoring latitude is Task 4 Step 2 (SKILL.md body) — bounded by an explicit 8-item non-negotiable checklist + the committed `DESIGN.md` as source text, plus a pass/fail oracle in Task 4 Step 4, so it is testable, not a placeholder.

**Type consistency:** `reduce_records` / `reduce_file` signatures and the `{role,text,timestamp,session_id,git_branch}` turn shape are identical across Tasks 1, 2, and the CLI. The candidate schema and triage vocabulary referenced in Task 4 match `DESIGN.md` verbatim.
