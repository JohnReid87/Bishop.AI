---
name: bish-triage
description: Interrogate a free-text bug description against the current Bishop workspace — walk a canonical bug checklist, validate the suspected cause against the repo via the Explore subagent, then push one structured `bug` card (or split into a `spike` + fix-stub pair when root cause is unconfirmed). Use when the user has a bug to file and wants it pinned down before it hits the board.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: workspace
bishop.command: /bish-triage
bishop.stage: true
bishop.stage_prompt: "Describe the bug — symptom, repro if known, and any stack trace."
---

**Orientation:** if `.bishop/BISHOP_CONTEXT.md` exists in the workspace, read it first — it documents this workspace's lanes, tags, and the safe `bishop` CLI subcommands. Bishop regenerates it on every launch so the content is current.

---

**Before interviewing, detect the active Bishop workspace:**

Run `bishop workspace current --json`.

- If the command exits non-zero or produces no output, STOP immediately and tell the user:

  > **Not in a Bishop workspace.** Run `bishop workspace list` to see available workspaces,
  > then `cd` into one of the listed paths and retry.

- If it succeeds, parse the JSON and extract:
  - `name` — the workspace name (shown to the user as confirmation)
  - `tags[].name` — available tag names (used to confirm `bug` and `spike` exist before push)
  - `lanes[].name` — available lane names (used to confirm `To Do` exists before push)

---

**Resolve the triage seed from `$ARGUMENTS`.** Two paths only — there is **no card-Number path**:

1. **`$ARGUMENTS` is non-empty free text** (the workspace-launch / staging-dialog path):
   - Use the text verbatim as the seed for the triage.

2. **`$ARGUMENTS` is empty** (skill was launched without arguments and the stage dialog was dismissed):
   - Ask in chat: "Describe the bug — symptom, repro if known, and any stack trace." and wait for the user's reply before proceeding.

There is no source card. Do not ask any closing card-action question.

---

## Phase 1 — canonical bug skeleton

Walk the user through the standard bug checklist, one question at a time. For each item, repeat back what you already extracted from their seed text and only ask for what's missing. Cover, in order:

1. **Repro steps** — numbered steps a fresh reader could follow.
2. **Environment / version** — OS, app version, branch / commit if known.
3. **Expected behaviour** — one sentence.
4. **Actual behaviour** — one sentence; if a stack trace exists, capture it verbatim.
5. **First seen** — when did this start (a date, a commit, a release).
6. **Frequency** — every time / intermittent / once-off.

Use `AskUserQuestion` **only** when the answer set is genuinely discrete (e.g. frequency: every-time / intermittent / once-off). For everything else (repro steps, expected/actual prose, version strings, dates), use free-text follow-up.

When asking severity (Phase 3 below), put your recommendation as the first option suffixed with " (Recommended)".

## Phase 2 — repo-hypothesis grill

Always spawn **exactly one** `Agent(subagent_type: "Explore")` run.

**Briefing inputs:**
- The user's full bug description from Phase 1.
- Extracted feature / file / symbol mentions (anything that looks like a type name, file path, command name, or distinctive identifier in the description).
- **Stack-trace pre-extraction.** If the description contains a stack trace — regex `^\s*at\s` (.NET), `Exception:`, or `panic:` — pre-extract the topmost user-code frame symbol (skip framework frames like `System.*`, `Microsoft.*`) and pass it as a **primary** search target.

**Brief the Explore agent to return** `file:line` excerpts (paths + line numbers + short snippets), not whole files.

From the returned excerpts, form **1–2 root-cause hypotheses**. Present them to the user and grill on which (if any) matches their mental model:

> Hypothesis A: `Foo.cs:42` does X when it should do Y because Z.
> Hypothesis B: …
>
> Which matches what you're seeing? (`A` / `B` / `neither — I can't tell yet` / free-text)

If the user explicitly says they cannot confirm or refute the hypothesis (the "neither" answer or equivalent), flag the triage as a **spike-split candidate** for Phase 3.

## Phase 3 — scope

Ask via `AskUserQuestion` how to shape the card:

- **fix-only** — one card, fix in place, no new tests.
- **fix + regression test (Recommended)** — one card, fix + a named regression test.
- **workaround + docs** — one card, document the workaround; no code change yet.
- **split into spike + fix-stub** — only offer this option when Phase 2 flagged the triage as a spike-split candidate.

## Severity

Ask exactly once via `AskUserQuestion`:

- blocker
- high
- medium
- low

Recommend a level based on the symptom (e.g. data loss → blocker; cosmetic glitch → low) and suffix the first option with " (Recommended)". The chosen value is written to the card body as `### Severity`. **Never** manipulate lane position based on severity — the card lands in `To Do` regardless.

---

## Dedupe check

Before pushing, run:

```bash
bishop card list --tag bug --json
```

Parse the JSON. For each card whose `isClosed` is `false` (i.e. not in `Done` / not closed), compare its `title` against the proposed title. A duplicate is plausible when **two or more significant tokens overlap** (ignoring stop-words like `the`, `a`, `in`, `when`, `is`, `fix`, `bug`, `error`).

If any plausible duplicate is found, surface the top candidate and ask:

> Possible duplicate: `#M` — \<existing title\>.
> (`push anyway` / `edit` to revise the new title / `skip` to abandon this triage)

- `push anyway` → proceed with the push.
- `edit` → ask for a new title, then re-run the dedupe check with the revised title.
- `skip` → STOP. Do not push. No summary table.

---

## Body template

Compose the card body using these required H3 sections, **in this order, with these exact headings**:

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
- All six required sections (`### Why`, `### Repro`, `### Expected vs Actual`, `### Hypothesis`, `### Severity`, `### Acceptance`) must be present even if a value is `unknown`.
- Omit `### Out of scope` and `### Related` when they add no value.
- Backtick file paths, identifiers, and CLI commands.

### Spike-split shape

When the user chose "split into spike + fix-stub" in Phase 3, produce **two cards**:

1. **Spike card** — tag `spike`, lane `To Do`. Same `### Why`, `### Repro`, `### Expected vs Actual`, `### Hypothesis`, `### Severity` sections. Replace `### Acceptance` with:

   > ### Acceptance
   > - Hypothesis confirmed or refuted with evidence (file:line excerpts or repro under instrumentation)
   > - File the fix card with concrete acceptance criteria

2. **Fix-stub card** — tag `bug`, lane `To Do`. Same body sections, but `### Acceptance` reads:

   > ### Acceptance
   > - TBD after spike #N

   Replace `#N` with the spike card's Number after the spike push completes. Push the spike first so its Number is available.

---

## Push

Pipe the body via stdin using a heredoc — do **not** escape newlines inline:

```bash
bishop card add --lane "To Do" --title "<Title>" --tag bug --description-file - --bottom << 'BODY'
### Why
<fill in>

### Repro
1. <fill in>

### Expected vs Actual
**Expected:** <fill in>
**Actual:** <fill in>

### Hypothesis
<fill in>

### Severity
<level>

### Acceptance
- <fill in>
BODY
```

For the spike-split, push the **spike first** (`--tag spike`), capture its Number from stdout, then push the fix-stub (`--tag bug`) with `### Acceptance` referencing `#N`.

---

## Summary table

After the push(es), print exactly this table — no closing card-action prompt:

| Card | Title | Tag |
|------|-------|-----|
| #N   | …     | bug |

For a spike-split, the table has two rows: the spike first, then the fix-stub.

---

ARGUMENTS: $ARGUMENTS
