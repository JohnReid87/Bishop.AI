---
name: bish-triage
description: Interrogate a free-text bug description against the current Bishop workspace — walk a canonical bug checklist, validate the suspected cause against the repo via the Explore subagent, then push one structured `bug` card (or split into a `spike` + fix-stub pair when root cause is unconfirmed). Use when the user has a bug to file and wants it pinned down before it hits the board.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: card,workspace
bishop.command: /bish-triage {{card_number}}
bishop.stage: true
bishop.stage_prompt: "Describe the bug — symptom, repro if known, and any stack trace."
bishop.category: review
---

> Recommended model: Sonnet 4.6 — structured bug-checklist walk; extended reasoning not required.

## What this skill is

A **bug-skeleton walk plus a scope decision**. The soul of the skill is
producing a *trustable* bug card — one where root cause has been examined
against the repo, the symptom is reproducible from the body, and the agent
that picks it up next knows whether it's a fix, a spike, or a workaround.

Three phases, always in order:

1. **Canonical bug skeleton** — walk the standard fields (repro, env,
   expected, actual, first-seen, frequency) one question at a time. Reuse
   what the seed already contains; only ask for what's missing.
2. **Repo-hypothesis grill** — exactly *one* Explore subagent run, forming
   1–2 root-cause hypotheses, presented for the user to confirm or refute.
   "I can't tell yet" flags a spike-split.
3. **Scope decision** — fix-only / fix + regression test / workaround +
   docs / spike-split (last option only when Phase 2 surfaced it).

Plus a one-shot **severity** ask with a recommended level based on the
symptom.

Before pushing, run a **dedupe check** against existing open `bug` cards.
The dedupe step is what stops the board accumulating five copies of the
same crash report.

The card lands in `To Do` regardless of severity — `bish-triage` never
manipulates lane position to signal urgency.

---

**Initialize from `bishop skill bootstrap`.** Run `bishop skill bootstrap --json`.
If it exits non-zero, surface the stderr line verbatim and STOP — the helper
already explains the remediation. On success, parse the JSON and capture
`workspaceName`, `tags[].name`, `lanes[].name` — confirm `bug`, `spike`, and
`To Do` are present before push.

> **Workspace:** \<workspaceName\>

---

**Resolve the triage seed from `$ARGUMENTS`.** Three paths:

1. **`$ARGUMENTS` is a card Number** (matches `^#?\d+$`, e.g. `42` or `#42`):
   - Strip a leading `#` if present.
   - Run `bishop card view <number> --json`.
   - If the command exits non-zero, STOP and surface stderr as-is. Do not
     guess.
   - Parse the JSON and capture `number`, `title`, `description`, `tag`,
     `laneName`.
   - Remember the `number` as the **source card** — reused in the closing
     prompt below.
   - Use `title` + `description` (verbatim, joined) as the bug-description
     seed for Phase 1.
   - Echo back so the user can confirm before the interview begins:

     > **Triaging card #N:** \<title\> *(lane: \<laneName\>, tag: \<tag\>)*

2. **`$ARGUMENTS` is non-empty free text** (workspace-launch / staging-dialog
   path):
   - Use the text verbatim as the seed. No source card; skip the closing
     prompt later.

3. **`$ARGUMENTS` is empty** (skill launched without arguments and the stage
   dialog was dismissed):
   - Ask in chat: "Describe the bug — symptom, repro if known, and any
     stack trace." and wait for the user's reply. No source card.

---

## Phase 1 — canonical bug skeleton

Walk the user through the standard bug checklist, one question at a time.
For each item, repeat back what you already extracted from the seed text
and only ask for what's missing. Cover, in order:

1. **Repro steps** — numbered steps a fresh reader could follow.
2. **Environment / version** — OS, app version, branch / commit if known.
3. **Expected behaviour** — one sentence.
4. **Actual behaviour** — one sentence; if a stack trace exists, capture
   it verbatim.
5. **First seen** — when did this start (a date, a commit, a release).
6. **Frequency** — every time / intermittent / once-off.

Use `AskUserQuestion` **only** when the answer set is genuinely discrete
(e.g. frequency: every-time / intermittent / once-off). For prose answers
(repro steps, expected/actual, version strings, dates), use free-text.

## Phase 2 — repo-hypothesis grill

Always spawn **exactly one** `Agent(subagent_type: "Explore")` run.

**Briefing inputs:**
- The user's full bug description from Phase 1.
- Extracted feature / file / symbol mentions (anything that looks like a
  type name, file path, command name, or distinctive identifier).
- **Stack-trace pre-extraction.** If the description contains a stack
  trace — regex `^\s*at\s` (.NET), `Exception:`, or `panic:` — pre-extract
  the topmost user-code frame symbol (skip framework frames like
  `System.*`, `Microsoft.*`) and pass it as a **primary** search target.

**Brief the Explore agent to return** `file:line` excerpts (paths + line
numbers + short snippets), not whole files.

From the returned excerpts, form **1–2 root-cause hypotheses**. Present
them to the user and grill on which (if any) matches their mental model:

> Hypothesis A: `Foo.cs:42` does X when it should do Y because Z.
> Hypothesis B: …
>
> Which matches what you're seeing? (`A` / `B` / `neither — I can't tell yet` / free-text)

If the user explicitly says they cannot confirm or refute the hypothesis
(the "neither" answer or equivalent), flag the triage as a **spike-split
candidate** for Phase 3.

## Phase 3 — scope

Ask via `AskUserQuestion` how to shape the card:

- **fix-only** — one card, fix in place, no new tests.
- **fix + regression test (Recommended)** — one card, fix + a named
  regression test.
- **workaround + docs** — one card, document the workaround; no code
  change yet.
- **split into spike + fix-stub** — only offer this option when Phase 2
  flagged the triage as a spike-split candidate.

## Severity

Ask exactly once via `AskUserQuestion`:

- blocker
- high
- medium
- low

Recommend a level based on the symptom (e.g. data loss → blocker;
cosmetic glitch → low) and suffix the first option with " (Recommended)".
The chosen value is written to the card body as `### Severity`. **Never**
manipulate lane position based on severity — the card lands in `To Do`
regardless.

---

## Dedupe check

Before pushing, run:

```bash
bishop card list --tag bug --json
```

Parse the JSON. If a source card was captured (Path 1) and it is
`bug`-tagged, exclude it from the candidate pool (`c.number != sourceNumber`)
so the source card is not flagged as a duplicate of itself. For each
remaining card whose `isClosed` is `false` (i.e. not in `Done` / not
closed), compare its `title` against the proposed title. A duplicate is
plausible when **two or more significant tokens overlap** (ignoring
stop-words like `the`, `a`, `in`, `when`, `is`, `fix`, `bug`, `error`).

If any plausible duplicate is found, surface the top candidate and ask:

> Possible duplicate: `#M` — \<existing title\>.
> (`push anyway` / `edit` to revise the new title / `skip` to abandon this triage)

- `push anyway` → proceed with the push.
- `edit` → ask for a new title, then re-run the dedupe check with the
  revised title.
- `skip` → STOP. Do not push. No summary table.

---

## Body template

Compose the card body using these required H3 sections, **in this order,
with these exact headings**:

```markdown
### Why
<1-sentence "X breaks when Y" — do not restate the title>

### Repro
1. <step>
2. <step>
…
Env: <OS / version / branch>

### Expected vs Actual
**Expected:** <one sentence>
**Actual:** <one sentence; include stack trace verbatim if any>

### Hypothesis
<root cause, with suspected `file.cs:NN`>

### Severity
blocker | high | medium | low

### Acceptance
- Fix in place at <file.cs:NN>
- Regression test named `<TestClass.TestMethod>` covers the broken path

### Out of scope
<optional — anything explicitly excluded>

### Related
<optional — prior card numbers, commit SHAs, issue links>
```

Rules:
- All six required sections (`### Why`, `### Repro`, `### Expected vs Actual`,
  `### Hypothesis`, `### Severity`, `### Acceptance`) must be present even
  if a value is `unknown`.
- Omit `### Out of scope` and `### Related` when they add no value.
- Backtick file paths, identifiers, and CLI commands.

### Spike-split shape

When the user chose "split into spike + fix-stub" in Phase 3, produce
**two cards**:

1. **Spike card** — tag `spike`, lane `To Do`. Same `### Why`, `### Repro`,
   `### Expected vs Actual`, `### Hypothesis`, `### Severity` sections.
   Replace `### Acceptance` with:

   > ### Acceptance
   > - Hypothesis confirmed or refuted with evidence (file:line excerpts or repro under instrumentation)
   > - File the fix card with concrete acceptance criteria

2. **Fix-stub card** — tag `bug`, lane `To Do`. Same body sections, but
   `### Acceptance` reads:

   > ### Acceptance
   > - TBD after spike #N

   Replace `#N` with the spike card's Number after the spike push
   completes. Push the spike first so its Number is available.

---

## Push

Add the card(s) using `bishop card add` per [bishop context print --section "Card Push Procedure"](.bishop/BISHOP_CONTEXT.md#card-push-procedure-stable) (STABLE). Always `--tag bug` (or `--tag spike`
for the spike half of a split) and `--lane "To Do"`.

For the spike-split, push the **spike first** (`--tag spike`), capture
its Number from stdout, then push the fix-stub (`--tag bug`) with
`### Acceptance` referencing `#N`.

---

## Summary table

After the push(es), print exactly this table:

| Card | Title | Tag |
|------|-------|-----|
| #N   | …     | bug |

For a spike-split, the table has two rows: the spike first, then the
fix-stub.

---

## Closing card-action prompt

If a **source card** was captured at the start (Path 1), prompt the user
about it after the summary, per [bishop context print --section "Source Card Closing Prompt"](.bishop/BISHOP_CONTEXT.md#source-card-closing-prompt-stable) (STABLE). For the spike-split shape, the
prompt fires once for the original source card (independent of the newly
created spike + fix-stub pair).

If there is no source card (Paths 2 and 3), skip this prompt entirely.

ARGUMENTS: $ARGUMENTS
