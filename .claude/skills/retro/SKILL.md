---
name: retro
description: >-
  Use after finishing a spec→plan→implement workflow (or any long/painful
  session), or whenever the user asks "how did that go", "let's do a retro",
  or wants to improve how they and the agent work together. Reviews the whole
  collaboration lifecycle to find concrete improvements. Triggers on: completed
  implementation work, sessions that took longer than they should have, repeated
  rework, mid-stream direction changes, or an explicit retro request. The agent
  should proactively OFFER a retro when an implement workflow just wrapped.
---

# Retro

## Overview

A blunt, evidence-based retrospective on a coding session, run to continuously
improve the workflow lifecycle from brainstorm → spec → plan → implement → verify.

**Core principle: a retro that finds nothing critical is a failed retro.** Every
session has friction. Your job is to find it, attribute it honestly to whoever
caused it — **the agent AND the user** — and turn each finding into a concrete,
durable fix. Praise is not the deliverable; correction is.

This skill diagnoses and *proposes*. It applies nothing without per-item approval.

## When to Use

- Right after a spec→plan→implement workflow completes (offer it proactively).
- After any session with rework, confusion, or that ran ~2x longer than it should.
- When the user says "retro", "how did that go", "what could we do better".

**When NOT to use:** trivial one-shot tasks with no friction, or mid-task (a retro
reviews completed work, it doesn't interrupt it).

## The Iron Law

```
NO RETRO WITHOUT AT LEAST ONE USER-ATTRIBUTED FINDING — OR AN EXPLICIT,
EVIDENCED STATEMENT THAT THE USER CAUSED ZERO FRICTION.
```

The default failure mode (proven by baseline testing) is the agent eating 100% of
the blame to be polite. That is sycophancy, and it is banned. If the user gave a
vague spec, changed direction mid-stream, skipped a review gate, or gave
contradictory instructions, **say so plainly with message-level evidence.** If
they genuinely caused no friction, you must state that explicitly — silence is
not allowed, because silence is how the rule gets dodged.

## Process

### 1. Gather the real material — don't trust a summary

Reason over the actual session. If the conversation was compacted, is very long,
or you're unsure of the exact sequence, **read the raw transcript** from
`~/.claude/projects/<project-slug>/*.jsonl` rather than a lossy memory of it.
Evidence (message-level: "at msg 6 you said X") is what makes findings
non-dismissible. Vague findings get ignored. If the transcript is genuinely
unavailable, say so up front and treat the retro as lower-confidence — never
present summary-only findings as if they had message-level evidence.

Also check what *should* have guided the work: the project CLAUDE.md, relevant
memories, and which skills were (or weren't) invoked.

### 2. Walk the lifecycle, phase by phase

For each phase, ask "what created friction here, and who caused it?"

| Phase | What to interrogate |
|-------|---------------------|
| Brainstorm | Was intent clear? Were assumptions surfaced or guessed? |
| Spec | Were acceptance criteria locked before work started? Ambiguous? |
| Plan | Was there a plan? Did it survive contact, or get abandoned? |
| Implement | Scope creep? Dead code left? Docs/conventions ignored? Rework? |
| Verify | Was "done" claimed without evidence? Edge cases missed until prompted? |

### 3. Critique both targets — bluntly, no hedging

- **Agent failures:** guessed instead of asking, skipped a skill, didn't read
  CLAUDE.md/memory, over-built, scope-crept, claimed done without verifying,
  performative agreement ("you're absolutely right!").
- **User failures:** one-line spec for a multi-decision task, direction change
  after work started, skipped review gate, contradictory or moving requirements,
  asking for speed then being surprised by missing rigor.

No diplomatic framing. Name it like a peer engineer in a blameless-but-honest
retro: state the fact, the cost, the evidence. **Every criticism must pair with a
concrete fix** — criticism without a remedy is banned.

### 4. For each finding, decide the BEST landing spot

Don't dump everything into one place. Reason about where each fix actually belongs
so it has effect next time:

| Finding type | Best landing spot |
|--------------|-------------------|
| Durable cross-session lesson about how to work with this user | **auto-memory** (feedback/user/project) — see the memory system |
| Project-specific convention, pattern, or rule | **project CLAUDE.md** |
| Recurring workflow gap that needs a repeatable procedure | **a personal/project skill** (edit existing or propose new) |
| A collaboration habit with no natural file home | **a behavioral note** — surfaced, not filed |

State the chosen target AND why. If a fix could go two places, pick one and justify.

**The behavioral-note escape hatch is the most-abused option.** A behavioral note
dies at the end of the session — it changes nothing next time. So you may NOT
route a finding to a behavioral note just because "the rule already exists and
adherence was the gap." The opposite is true: **a finding that repeats a rule
that already exists and was ignored is proof the rule isn't landing** — that
warrants a *stronger* placement (escalate it into memory, sharpen the CLAUDE.md
wording, or propose a hook), never a no-op note. Reserve behavioral notes only
for genuinely one-off, this-session-only observations with no durable lesson.

### 5. Present findings — then STOP for approval

Output a structured list. Use this shape per finding:

```
[Phase] [severity: high/med/low] — short title
  What:     what happened
  Evidence: message refs / file / quote
  Cause:    agent | user | both
  Fix:      the concrete change to make next time
  Lands in: auto-memory | CLAUDE.md | skill <name> | behavioral note  (+ why)
```

Group into **Agent-caused** and **User-caused**. End with a one-line proposal of
which items to apply.

**Apply nothing yet.** Ask the user to approve per item (or "all" / "none"). Only
after approval do you write the memory entry, edit CLAUDE.md, or edit/create the
skill — following each target's own conventions (e.g. the auto-memory two-step:
write the file, add the MEMORY.md pointer).

## Decoupling from superpowers (hard requirement)

This skill must survive a superpowers plugin update untouched.

- Reference superpowers skills by **name only** (e.g. "the brainstorming skill"),
  never by path, never with `@`-links.
- **Never** edit, move, or depend on the contents of anything under
  `plugins/cache/`. Read them if useful, but propose changes only to YOUR files
  (memory, project CLAUDE.md, project/personal skills).

## Red Flags — you are being sycophantic, STOP

| Thought / output | Reality |
|------------------|---------|
| "Overall this went really well!" | You're opening with praise to soften. Lead with the worst finding. |
| All findings start with "I…" | You're eating the blame. Re-check for user-caused friction (Iron Law). |
| "Minor nitpick, but…" | If it cost time, it's not minor. State the cost. |
| No message/file evidence | Unevidenced findings get dismissed. Go read the transcript. |
| "Want me to capture this somewhere?" | Vague. Propose the specific target and diff. |
| Finding with no fix | Banned. Every finding pairs with a concrete remedy. |
| Skipped the user-friction check entirely | Iron Law violation. Either name it or explicitly clear them. |
| Applied a change before approval | Diagnose + propose only. Approval gates every write. |
| "It's a behavioral note — the rule already exists" | A repeated, ignored rule is escalation evidence, not a no-op. Place it stronger. |

## Common Mistakes

- **Trusting the compacted summary instead of the transcript** → findings are
  vague and wrong on detail. Read the `.jsonl`.
- **One generic "improve communication" finding** → useless. Be specific and
  evidenced, or drop it.
- **Filing everything into CLAUDE.md** → it bloats and gets loaded every session.
  Cross-session collaboration lessons belong in auto-memory; only project rules
  belong in CLAUDE.md.
- **Proposing a brand-new skill for a one-off** → a behavioral note or memory is
  usually enough. New skill only for genuinely recurring procedures.
