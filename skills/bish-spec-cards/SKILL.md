---
name: bish-spec-cards
description: Interrogate the user about a feature spec markdown file to generate implementation cards. Use when the user has a written feature description and wants to break it into Bishop cards, or invokes /bish-spec-cards.
allowed-tools: Read, Glob, Grep, Write, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: workspace
bishop.command: /bish-spec-cards
bishop.stage: true
bishop.stage_prompt: "Path to feature spec markdown file"
bishop.category: discuss
---

> Recommended model: Opus 4.7 — interrogating a document for gaps and missing acceptance criteria requires sustained multi-step judgement.

## What this skill is

A **document-seeded interrogation**. The user provides a markdown file describing a feature; the skill reads it and grills the user about what is missing, ambiguous, or underspecified — working through the document until every implementation decision has either an answer or an explicit out-of-scope statement. Cards are generated from what remains.

The soul of the skill is the **quality bar of the interrogation**: it must not accept the spec at face value, must probe every gap, and must not generate cards until acceptance criteria are verifiable.

The contract:

- **Document-first.** The spec drives the agenda. Work through it section by section rather than asking open-ended questions from a blank slate.
- **Probe for gaps.** For every behaviour described, ask: what does success look like? What are the edge cases? What is explicitly out of scope? What are the dependencies?
- **One question at a time.** No batched lists. The user steers; the agent narrows.
- **Always offer a recommended answer.** Every discrete question carries your best guess as the first option suffixed `" (Recommended)"`. Use `AskUserQuestion` when the answer set is bounded; free-text only for genuinely open questions.
- **No cards during the interview.** Collect answers first; propose cards at wrap-up only.

Cards land on the board only after **two gates**:

1. A **granularity pass** — see `Card Granularity Rules` (in `conventions`).
2. A **preview** the user explicitly confirms with `push`. The agent never writes to the board on its own.

The context-pack below bundles workspace metadata, recent git history, card data (when a card number is supplied), and Bishop convention procedures (Shell selection, Card Granularity Rules, Task List Preview Format, Card Push Procedure, Source Card Closing Prompt) — canonical source: `.bishop/BISHOP_CONTEXT.md`.

---

**Before anything else — load the context-pack:**

**Path A — `$ARGUMENTS` is a card Number** (matches `^#?\d+$`, e.g. `42` or `#42`):
- Strip a leading `#` if present, then call:
  ```
  bishop context-pack spec-cards --card <number>
  ```
  If the command exits non-zero, surface the stderr message as-is and STOP.

  Parse the JSON. `skill_specific.card` carries the loaded card.
  Remember `skill_specific.card.number` as the **source card** — reused in the closing prompt below.
  Use `skill_specific.card.description` as the spec body (and `skill_specific.card.title` for context).
  Echo back so the user can confirm before the interview begins:

  > **Spec from card #N:** \<title\> *(lane: \<lane_name\>, tag: \<tag\>)*

**Path B — `$ARGUMENTS` is non-empty free text** (file path from staging dialog):
```
bishop context-pack spec-cards
```
Treat `$ARGUMENTS` as a file path. Read the file with the `Read` tool. If the file does not exist, tell the user and STOP:

> **File not found:** `<path>`. Check the path and re-run.

Echo back once loaded:

> **Spec loaded:** `<path>`

No source card; skip the closing prompt later.

**Path C — `$ARGUMENTS` is empty** (staging dialog dismissed):
```
bishop context-pack spec-cards
```
Ask in chat: "What is the path to your feature spec file?" and wait for the user's reply. Read the file as in Path B. No source card.

In all paths, parse the JSON and extract:
- `workspace.name` — echoed back as confirmation
- `workspace.tags` — existing tag names
- `workspace.lanes` — lane names
- `conventions` — STABLE/TUNABLE procedure sections (Card Granularity Rules, Task List Preview Format, Card Push Procedure, Source Card Closing Prompt)

> **Workspace:** \<workspace.name\>

---

## The interview

Before speaking, scan the full spec and build a mental map:
- The feature's stated goal and scope boundary
- Any explicit acceptance criteria or "done means" statements already present
- Sections that are vague, missing, or that a developer would need to make a judgment call about
- Any dependencies on other systems, components, or prior work
- Error cases and edge cases mentioned — and those notably absent

Then work through the document with the user. For each section or topic:

1. Summarise what the spec says in one sentence.
2. Ask the single most important missing or ambiguous question about it.
3. When the user answers, either probe deeper if the answer raises new questions, or move to the next topic.

Probe specifically for:

- **Acceptance criteria** — "how will we know this is done?" for each described behaviour. If the spec already states criteria, verify they are testable.
- **Edge cases** — what happens when input is empty, malformed, absent, or at volume? What happens on failure?
- **Out of scope** — is there anything adjacent that is explicitly not being built now? Surface implicit boundaries and get them stated.
- **Dependencies** — does this require prior work, data that may not exist yet, or external components?
- **Breaking changes** — does this alter existing behaviour that other parts of the codebase depend on?
- **Implementation approach** — where the spec is silent on how, surface the key choices and agree the direction before generating cards.

When a question is answerable from the codebase, delegate to the **Explore subagent** (`Agent` with `subagent_type: "Explore"` and `model: "haiku"`). Brief it with the specific question and ask for `file:line` excerpts — never pull whole files into this conversation.

---

## After shared understanding — granularity pass + preview

Apply the heuristics in `Card Granularity Rules` (in `conventions`) to merge or split the proposed cards before previewing them.

Then print the preview in the shape defined by `Task List Preview Format` (in `conventions`). Each card uses the body template below.

**Tag** must be one of `workspace.tags` from the context-pack. Default to `feature`; the user may specify another.

**Lane** defaults to `To Do`. Use another lane name from `workspace.lanes` (typically `Backlog`) only when the user asks for a parking spot.

### Body template

```markdown
### Why
<what this task does, 1 sentence — do not restate the title>

### Changes
- <specific change>

### Decided
<choice> over <alt> because <reason>

### Acceptance
- <acceptance criterion drawn from the interview — verifiable>

### Out of scope
<anything explicitly excluded during the interview>

### Related
<links or card numbers>
```

Rules:
- `### Acceptance` is required — every card must have at least one verifiable criterion from the interview.
- Omit `### Decided` if no real tradeoff was discussed — don't invent one.
- Omit `### Changes`, `### Out of scope`, `### Related` when they add no value.
- Do not restate the title in `### Why`.
- Backtick file paths, identifiers, and CLI commands.

After the preview, ask:

> "Please review the tasks above. Say **push** to create the Bishop cards."

Do NOT push automatically. Wait for the user to say "push".

---

## Push

When the user confirms, add each card in order using `bishop card add` per `Card Push Procedure` (in `conventions`). Push with `--bottom` so cards land in agreed order.

After all cards are created, print a brief summary:

| Card | Title | Lane | Tag |
|------|-------|------|-----|
| #N   | …     | To Do | feature |

---

## Closing card-action prompt

If a **source card** was captured at the start (Path A), prompt the user about it after the summary per `Source Card Closing Prompt` (in `conventions`). If there is no source card (Paths B and C), skip this prompt entirely.

ARGUMENTS: $ARGUMENTS
