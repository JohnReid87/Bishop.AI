---
name: bish-grill-me
description: Interview the user relentlessly about a plan or design targeting the current Bishop workspace, then push the agreed cards directly to the board after confirmation. Use when working inside a Bishop workspace and the user wants to stress-test a plan, get grilled on their design, or mentions "grill me".
allowed-tools: Read, Glob, Grep, Write, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: card,workspace
bishop.command: /bish-grill-me {{card_number}}
bishop.stage: true
bishop.stage_prompt: "What do you want me to grill you on?"
bishop.category: discuss
---

> Recommended model: Opus 4.7 — relentless interview requires sustained multi-step judgement.

## What this skill is

A **relentless interview**. The soul of the skill is the quality bar of the
conversation, not the workspace plumbing wrapped around it.

Relentless means:

- **Walk every branch.** When a decision has dependent sub-decisions, resolve
  each one before moving on. Don't paper over forks with "we can decide that
  later."
- **One question at a time.** No batched lists of questions. The user steers;
  the agent narrows.
- **Always offer a recommended answer.** Every question carries your best
  guess as the first option — the user confirms or overrides, never picks
  blind. Use `AskUserQuestion` whenever the answer set is discrete; free-text
  only when genuinely open-ended (naming, novel design, free-form
  constraints).
- **Explore the codebase, don't read it.** When a question is answerable
  from the repo, delegate to the `Agent` tool with
  `subagent_type: "Explore"` and ask for `file:line` excerpts. Only fall
  back to direct Read/Grep for tight follow-ups on a file/line range Explore
  already surfaced.

Cards land on the board only after **two gates**:

1. A **granularity pass** — see `Card Granularity Rules` (in `conventions`).
2. A **preview** the user explicitly confirms with `push`. The agent never
   writes to the board on its own.

The context-pack below bundles workspace metadata, recent git history, card data (when a card number is supplied), and Bishop convention procedures (Shell selection, Card Granularity Rules, Task List Preview Format, Card Push Procedure, Source Card Closing Prompt) — canonical source: `.bishop/BISHOP_CONTEXT.md`.

---

**Before anything else — load the context-pack:**

**Path A — `$ARGUMENTS` is a card Number** (matches `^#?\d+$`, e.g. `42` or `#42`):
- Strip a leading `#` if present, then call:
  ```
  bishop context-pack grill-me --card <number>
  ```
  If the command exits non-zero, surface the stderr message as-is and STOP.
  
  Parse the JSON. `skill_specific.card` carries the loaded card.
  Remember `skill_specific.card.number` as the **source card** — reused in the closing prompt below.
  Use `skill_specific.card.title` + `skill_specific.card.description` (and tag / laneName for context) as the seed for the grill.
  Echo back so the user can confirm before the interview begins:

  > **Grilling card #N:** \<title\> *(lane: \<laneName\>, tag: \<tag\>)*

**Path B — `$ARGUMENTS` is non-empty free text** (workspace-launch / staging-dialog path):
```
bishop context-pack grill-me
```
Use the text verbatim as the seed. No source card; skip the closing prompt later.

**Path C — `$ARGUMENTS` is empty** (skill launched without arguments and the stage dialog was dismissed):
```
bishop context-pack grill-me
```
Ask in chat: "What should I grill you on?" and wait for the user's reply before proceeding. No source card.

In all paths, parse the JSON and extract:
- `workspace.name` — echoed back as confirmation
- `workspace.tags` — existing tag names
- `workspace.lanes` — lane names
- `conventions` — STABLE/TUNABLE procedure sections (Shell selection, Card Granularity Rules, Task List Preview Format, Card Push Procedure, Source Card Closing Prompt)

> **Workspace:** \<workspace.name\>

---

## The interview

Interview the user relentlessly about every aspect of the seed until shared
understanding is reached. Walk down each branch of the design tree, resolving
dependencies between decisions one by one. For each question, provide your
recommended answer; put it as the first option in `AskUserQuestion` and
suffix it with " (Recommended)".

When a question is answerable from the codebase, delegate to the **Explore
subagent** (via the `Agent` tool with `subagent_type: "Explore"`). Brief it
with the specific question and ask it to return `file:line` excerpts — never
pull whole files into this conversation.

---

## After shared understanding — granularity pass + preview

Apply the heuristics in `Card Granularity Rules` (in `conventions`) to merge or split the proposed cards
before previewing them.

Then print the preview in the shape defined by `Task List Preview Format` (in `conventions`). Each card uses the body template
below.

**Tag** must be one of `workspace.tags` from the context-pack. If the
workspace has no tags defined, use `feature` as default.

**Lane** defaults to `To Do`. Use another lane name from `workspace.lanes`
(typically `Backlog`) only when the user asks for a parking spot.

### Body template

```markdown
### Why
<what this task does, 1 sentence — do not restate the title>

### Changes
- <specific change>

### Decided
<choice> over <alt> because <reason>

### Acceptance
- <acceptance criterion>

### Out of scope
<anything explicitly excluded>

### Related
<links or card numbers>
```

Rules:
- Omit `### Decided` if no real tradeoff was discussed — don't invent one.
- Omit `### Changes`, `### Out of scope`, `### Related` when they add no value.
- Do not restate the title in `### Why`.
- Backtick file paths, identifiers, and CLI commands.

After the preview, ask:

> "Please review the tasks above. Say **push** to create the Bishop cards."

Do NOT push automatically. Wait for the user to say "push".

---

## Push

When the user confirms, add each card in order using `bishop card add` per
`Card Push Procedure` (in `conventions`). Push with `--bottom`
so cards land in agreed order.

After all cards are created, print a brief summary:

| Card | Title | Lane | Tag |
|------|-------|------|-----|
| #N   | …     | …    | …   |

---

## Closing card-action prompt

If a **source card** was captured at the start (Path A), prompt the user
about it after the summary, per `Source Card Closing Prompt` (in `conventions`). If there is no source card
(Paths B and C), skip this prompt entirely.

ARGUMENTS: $ARGUMENTS
