---
name: bish-chat
description: Quick open-ended chat about a single Bishop card. Lightweight sibling of /bish-grill-me for revisiting one specific card — an old card that didn't quite meet requirements, or a spike that needs open-ended discussion before scope is fixed. Use when the user wants to talk through one card in the current workspace without committing to a full grill-me task list up front.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: card
bishop.command: /bish-chat {{card_number}}
bishop.stage: false
bishop.category: discuss
---

## What this skill is

A **single-card, non-mutating chat**. The soul of the skill is what it
refuses to do as much as what it does.

The chat contract:

- **One card per session.** `$ARGUMENTS` is a required card Number.
  Free-text seeds belong in `/bish-grill-me`.
- **Never move the source card.** No `card move`, no `card close`, no
  `card reopen` at any point. The card stays in whatever lane it started
  in. Lane changes are out of scope.
- **Never split or delete the source card.**
- **Lazy Explore.** Do not spawn the Explore subagent at chat start. Only
  delegate when an actual code question comes up. Many chats never need it.
- **Drift-tolerant.** Open-ended conversation. Resolve each branch before
  moving to the next, but don't force a checklist — the user is thinking
  out loud.

The wrap-up produces **one combined proposal** covering source-card edits
*and* any follow-up cards. The user reviews and says `push` before
anything is written to the board.

---

**Initialize from `bishop skill bootstrap`.** Run `bishop skill bootstrap --json`.
If it exits non-zero, surface the stderr line verbatim and STOP — the helper
already explains the remediation. On success, parse the JSON and capture
`workspaceName`, `tags[].name`, `lanes[].name` for the wrap-up step.

> **Workspace:** \<workspaceName\>

---

**Resolve the card from `$ARGUMENTS`.**

`$ARGUMENTS` is **required** and must match `^#?\d+$` (e.g. `42` or `#42`).

- If `$ARGUMENTS` is empty, ask in chat:

  > Which card? (paste a Number, e.g. `42` or `#42`)

  Wait for the user's reply before continuing.

- If `$ARGUMENTS` is non-empty but does not match the pattern, STOP and
  tell the user:

  > `bish-chat` accepts a single card Number only (e.g. `42` or `#42`).
  > Use `/bish-grill-me` for free-text seeds.

- Strip a leading `#` if present, then run:

  ```
  bishop card view <number> --json
  ```

  If the command exits non-zero, STOP and surface stderr as-is. Do NOT
  guess.

  Parse the JSON and capture:
  - `number` — the canonical `#N` reference (used in headings and the
    `### Related` line)
  - `title`, `description`, `laneName`, `tags`

  Remember this as the **source card** — you will use `number` (and the
  existing `tags` list) in the wrap-up.

Echo back so the user can confirm the right card was loaded:

> **Card #N:** \<title\> *(lane: \<laneName\>, tags: \<comma-joined or "none"\>)*

Then open the chat with:

> What are you thinking?

---

## The chat

Hold an open-ended conversation about the card. Resolve each branch of the
discussion before moving to the next, the same way `/bish-grill-me` does —
but without forcing a structured task-tree walk.

When a question is answerable from the repo, delegate to the **Explore
subagent** (via `Agent` with `subagent_type: "Explore"`) for `file:line`
excerpts. Do not Read large files directly. **Do not run Explore eagerly at
chat start** — only when an actual code question arises.

Prefer `AskUserQuestion` over free-text prompts whenever a question has a
discrete set of answers (pick a tag, choose between approaches, yes/no on a
tradeoff). Put your recommendation as the first option suffixed with
" (Recommended)". Free-text is fine when the answer is genuinely
open-ended.

---

## Reaching wrap-up

Move to the wrap-up when either:

- The conversation reaches a natural pause and there's a clear set of next
  actions (or a clear conclusion that nothing needs to change), **or**
- The user says something like "wrap up", "land it", "ready", "let's push",
  or similar.

Produce a **single combined proposal**:

1. **Source-card edits** — zero or more of: title change, description
   rewrite, tag list change. Only include fields the chat actually decided
   to change. Tag changes pass the **full intended list** (existing tags
   you want to keep + any new ones); `bishop card edit --tag` replaces all
   tags.
2. **Follow-up cards** — zero or more new cards spun out of the chat.

Apply the heuristics in [Card Granularity Rules](.bishop/BISHOP_CONTEXT.md#card-granularity-rules-tunable) before listing follow-ups.

Each follow-up card's body uses the template below, plus an auto-included
`### Related` section linking back to the source card (`#<source-number>`).

**Tag** must be one of `tags[].name` from the bootstrap JSON. If the
workspace has no tags, default to `feature`. **Lane** defaults to `To Do`;
use another lane name from `lanes[].name` only when placing the card
somewhere other than the default.

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
- #<source-number>
```

Rules:
- Omit `### Decided` if no real tradeoff was discussed — don't invent one.
- Omit `### Changes` and `### Out of scope` when they add no value.
- Do not restate the title in `### Why`.
- Backtick file paths, identifiers, and CLI commands.

---

## Print the proposal

> **Source card #N — proposed changes**
>
> *(omit any of the three sub-sections that have no changes)*
>
> - **Title:** \<old\> → \<new\>
> - **Description:** *(short diff or "rewritten — see below")*
> - **Tags:** \<old comma-joined\> → \<new comma-joined\>
>
> ```markdown
> <new description body, if changed>
> ```
>
> **Follow-up cards** — render per [Task List Preview Format](.bishop/BISHOP_CONTEXT.md#task-list-preview-format-stable).

Either side may be empty. If the chat concluded that nothing needs to
change, print:

> Nothing to change on **#N** and no follow-ups to file. Done.

…and STOP — do not prompt for `push` when there's nothing to apply.

Otherwise, ask:

> Please review. Say **push** to apply.

Do NOT push automatically.

---

## Push

When the user confirms with `push`:

1. **Apply source-card edits first** (skip if no edits proposed). Build a
   single `bishop card edit` call with only the changed flags:

   - `--title "<new>"` when the title changed.
   - `--tag "<name>"` repeated per tag when the tag list changed (pass the
     **full intended list**, not just additions; the flag replaces all
     tags). Use `--clear-tags` instead if the chat decided to remove all
     tags.
   - `--description-file -` when the description changed; pipe the new
     body via a single-quoted heredoc so `$` and backticks stay literal.

   Example with all three changes:

   ```bash
   bishop card edit <number> --title "<new>" --tag "<tagA>" --tag "<tagB>" --description-file - << 'BODY'
   <new description body>
   BODY
   ```

2. **Add each follow-up card** in order using `bishop card add` per
   [Card Push Procedure](.bishop/BISHOP_CONTEXT.md#card-push-procedure-stable). Push with
   `--bottom`.

3. **Print a summary table** covering both the source-card edit (if any)
   and the new cards:

   | Card | Action | Title | Lane | Tag |
   |------|--------|-------|------|-----|
   | #<source> | edit | <new title or "unchanged"> | <lane> | <tags> |
   | #<new>    | add  | … | … | … |

`bish-chat` deliberately omits the source-card closing prompt that
`bish-grill-me` and `bish-triage` use — chat never moves the source card.

ARGUMENTS: $ARGUMENTS
