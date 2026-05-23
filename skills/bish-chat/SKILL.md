---
name: bish-chat
description: Quick open-ended chat about a single Bishop card. Lightweight sibling of /bish-grill-me for revisiting one specific card — an old card that didn't quite meet requirements, or a spike that needs open-ended discussion before scope is fixed. Use when the user wants to talk through one card in the current workspace without committing to a full grill-me task list up front.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: card
bishop.command: /bish-chat {{card_number}}
bishop.stage: false
bishop.category: discuss
---

**Orientation:** if `.bishop/BISHOP_CONTEXT.md` exists in the workspace, read it first — it documents this workspace's lanes, tags, and the safe `bishop` CLI subcommands. Bishop regenerates it on every launch so the content is current.

---

**Before anything else — detect the active Bishop workspace:**

Run `bishop workspace current --json`.

- If the command exits non-zero or produces no output, STOP immediately and tell the user:

  > **Not in a Bishop workspace.** Run `bishop workspace list` to see available workspaces,
  > then `cd` into one of the listed paths and retry.

- If it succeeds, parse the JSON and extract:
  - `name` — the workspace name
  - `tags[].name` — available tag names (for the wrap-up step)
  - `lanes[].name` — available lane names (for the wrap-up step)

---

**Resolve the card from `$ARGUMENTS`.**

`$ARGUMENTS` is **required** and must match `^#?\d+$` (e.g. `42` or `#42`).

- If `$ARGUMENTS` is empty, ask in chat:

  > Which card? (paste a Number, e.g. `42` or `#42`)

  Wait for the user's reply before continuing.

- If `$ARGUMENTS` is non-empty but does not match the pattern, STOP and tell the user:

  > `bish-chat` accepts a single card Number only (e.g. `42` or `#42`). Use `/bish-grill-me`
  > for free-text seeds.

- Strip a leading `#` if present, then run:

  ```
  bishop card view <number> --json
  ```

  If the command exits non-zero (no match), STOP and surface the stderr as-is. Do NOT guess.

  Parse the JSON and capture:
  - `number` — the canonical `#N` reference (used in headings and the `### Related` line)
  - `title`, `description`, `laneName`, `tags`

  Remember this as the **source card** — you will use `number` (and the existing
  `tags` list) in the wrap-up step.

Echo back so the user can confirm the right card was loaded:

> **Card #N:** \<title\> *(lane: \<laneName\>, tags: \<comma-joined or "none"\>)*

Then open the chat with:

> What are you thinking?

**Never auto-move the card.** Chat is exploratory, not execution. The source card
stays in whatever lane it was in.

---

**During the chat:**

Hold an open-ended conversation about the card. Resolve each branch of the
discussion before moving to the next, the same way `/bish-grill-me` does.

If a question can be answered by exploring the codebase, delegate that to the
**Explore subagent** (via the `Agent` tool with `subagent_type: "Explore"`) — do
not Read large files into this conversation directly. Brief it with the specific
question you need answered and ask it to return file paths + line numbers +
short excerpts. Only fall back to direct Read/Grep for tight follow-ups on a
file/line range the Explore agent already surfaced.

Do **not** run Explore eagerly at chat start — only when an actual code question
arises. Many chats never need it.

**Prefer `AskUserQuestion` over free-text prompts** whenever a question has a
discrete set of plausible answers (pick a tag, choose between approaches, yes/no
on a tradeoff). Put your recommendation as the first option and suffix it with
" (Recommended)". Free-text is fine when the answer is genuinely open-ended.

---

**Reaching wrap-up.**

Move to the wrap-up step when either:

- The conversation reaches a natural pause and there's a clear set of next
  actions (or a clear conclusion that nothing needs to change), **or**
- The user says something like "wrap up", "land it", "ready", "let's push", or
  similar.

In the wrap-up, produce a **single combined proposal** covering both sides:

1. **Source-card edits** — zero or more of: title change, description rewrite,
   tag list change. Only include fields that the chat actually decided to
   change. Tag changes must pass the **full intended tag list** (existing tags
   you want to keep + any new ones); `bishop card edit --tag` replaces all tags.
2. **Follow-up cards** — zero or more new cards spun out of the chat.

Apply the same **granularity pass** as `/bish-grill-me` before listing follow-ups:

- One card ≈ one PR's worth of work. Fold one-line or sub-30-minute changes
  into the nearest related card.
- Merge cards that touch the same file/module for the same reason.
- Split only when the pieces have independent acceptance criteria or could
  ship in separate PRs without one blocking the other.

Each follow-up card's body uses the same H3-section template as `/bish-grill-me`,
plus an auto-included `### Related` section linking back to the source card
(`#<source-number>`).

**Tag** should match one of the tag names from `tags[].name` in the workspace JSON.
If the workspace has no tags defined, use `feature` as the default.

**Lane** defaults to `To Do`. Use a lane name from `lanes[].name` when placing
the card somewhere other than the default.

**Body format** (required sections: `### Why`, `### Acceptance`, `### Related`):

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

**Print the proposal for review.**

> **Source card #N — proposed changes**
>
> *(omit any of the three sub-sections that have no changes)*
>
> - **Title:** \<old\> → \<new\>
> - **Description:** *(show a short diff or "rewritten — see below")*
> - **Tags:** \<old comma-joined\> → \<new comma-joined\>
>
> ```markdown
> <new description body, if changed>
> ```
>
> **Follow-up cards**
>
> ## Tasks
> - [ ] **Title**: \<concise card title\> | **Tag**: \<tag\> | **Lane**: \<lane\> | **Body**: \<body\>
> - [ ] **Title**: ...

Either side may be empty. If the chat concluded that nothing needs to change,
print:

> Nothing to change on **#N** and no follow-ups to file. Done.

…and STOP — do not prompt for `push` when there's nothing to apply.

Otherwise, ask:

> Please review. Say **push** to apply.

Do NOT push automatically. Wait for the user to say "push".

---

**When the user confirms with `push`:**

1. **Apply source-card edits first** (skip if no edits proposed). Build a
   single `bishop card edit` call with only the changed flags:

   - `--title "<new>"` when the title changed.
   - `--tag "<name>"` repeated per tag when the tag list changed (pass the
     **full intended list**, not just additions; the flag replaces all tags).
     Use `--clear-tags` instead if the chat decided to remove all tags.
   - `--description-file -` when the description changed; pipe the new body
     via a single-quoted heredoc so `$` and backticks stay literal.

   Example with all three changes:

   ```bash
   bishop card edit <number> --title "<new>" --tag "<tagA>" --tag "<tagB>" --description-file - << 'BODY'
   <new description body>
   BODY
   ```

2. **Add each follow-up card in order**, piping each body via stdin:

   ```bash
   bishop card add --lane "<Lane>" --title "<Title>" --tag "<Tag>" --description-file - --bottom << 'BODY'
   ### Why
   <fill in>

   ### Acceptance
   - <fill in>

   ### Related
   - #<source-number>
   BODY
   ```

3. **Print a summary table** covering both the source-card edit (if any) and
   the new cards:

   | Card | Action | Title | Lane | Tag |
   |------|--------|-------|------|-----|
   | #<source> | edit | <new title or "unchanged"> | <lane> | <tags> |
   | #<new>    | add  | ... | ... | ... |

---

**Guardrails:**

- Never move the source card to a different lane (no `card move`, no `card
  close`, no `card reopen`) at any point. Lane changes are out of scope.
- Never split or delete the source card.
- Do not run the Explore subagent eagerly at chat start — only when an actual
  code question comes up.
- Do not accept free-text seeds. If the user wants to seed a chat from a
  description rather than a card, tell them to use `/bish-grill-me`.

ARGUMENTS: $ARGUMENTS
