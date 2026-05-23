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

**Orientation:** if `.bishop/BISHOP_CONTEXT.md` exists in the workspace, read it first — it documents this workspace's lanes, tags, and the safe `bishop` CLI subcommands. Bishop regenerates it on every launch so the content is current.

---

**Before interviewing, detect the active Bishop workspace:**

Run `bishop workspace current --json`.

- If the command exits non-zero or produces no output, STOP immediately and tell the user:

  > **Not in a Bishop workspace.** Run `bishop workspace list` to see available workspaces,
  > then `cd` into one of the listed paths and retry.

- If it succeeds, parse the JSON and extract:
  - `name` — the workspace name (shown to the user as confirmation)
  - `tags[].name` — available tag names (offer as choices during the interview)
  - `lanes[].name` — available lane names (offer as choices during the interview)

---

**Resolve the grill seed from `$ARGUMENTS`.** Three paths:

1. **`$ARGUMENTS` is a card Number** (matches `^#?\d+$`, e.g. `42` or `#42`):
   - Strip a leading `#` if present.
   - Run `bishop card view <number> --json`.
   - If the command exits non-zero, STOP and surface stderr as-is. Do not guess.
   - Parse the JSON and capture `number`, `title`, `description`, `tags`, `laneName`.
   - Remember the `number` as the **source card** — you will reuse it at the end of the flow.
   - Use `title` + `description` (and tags / lane for context) as the seed for the grill.
   - Echo back so the user can confirm before the interview begins:

     > **Grilling card #N:** \<title\> *(lane: \<laneName\>, tags: \<comma-joined\>)*

2. **`$ARGUMENTS` is non-empty free text** (the workspace-launch / staging-dialog path):
   - Use the text verbatim as the seed for the grill.
   - There is no source card; skip the closing card-action prompt later.

3. **`$ARGUMENTS` is empty** (skill was launched without arguments and the stage dialog was dismissed):
   - Ask in chat: "What should I grill you on?" and wait for the user's reply before proceeding.
   - There is no source card.

---

Interview me relentlessly about every aspect of this plan until
we reach a shared understanding. Walk down each branch of the design
tree resolving dependencies between decisions one by one.

If a question can be answered by exploring the codebase, explore
the codebase instead — but delegate that exploration to the **Explore
subagent** (via the `Agent` tool with `subagent_type: "Explore"`)
rather than reading files into this conversation directly. Brief it
with the specific question you need answered and ask it to return
file paths + line numbers + short excerpts. Only fall back to direct
Read/Grep for tight follow-ups on a file/line range the Explore
agent already surfaced.

For each question, provide your recommended answer.

**Prefer `AskUserQuestion` over free-text prompts** whenever the
question has a discrete set of plausible answers — pick a tag, choose
between architectural patterns, decide on a lane, yes/no on a
tradeoff. Put your recommendation as the first option and suffix it
with " (Recommended)". Only fall back to free-text when the answer
is genuinely open-ended (naming, novel design, free-form constraints).

When we have reached shared understanding, do a **granularity pass**
before writing the task list:

- One task ≈ one PR's worth of work. If a task is a one-line change or
  under ~30 minutes, fold it into the nearest related task rather than
  filing it standalone.
- Merge tasks that touch the same file/module for the same reason.
- Split only when the pieces have independent acceptance criteria or
  could ship in separate PRs without one blocking the other.

Then print the task list for review:

> **Target workspace:** <name>

## Tasks
- [ ] **Title**: <concise card title> | **Tag**: <tag> | **Lane**: <lane> | **Body**: <body>
- [ ] **Title**: ...

**Tag** should match one of the tag names from `tags[].name` in the workspace JSON.
If the workspace has no tags defined, use `feature` as the default.

**Lane** defaults to `To Do`. Use a lane name from `lanes[].name` when placing
the card somewhere other than the default.

**Body format.** Use the H3-section markdown template below. Required sections: `### Why` and `### Acceptance`. Include optional sections only when they add value.

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
- Use bullets in `### Changes` and `### Acceptance`.

After printing the task list, ask:
> "Please review the tasks above. Say **push** to create the Bishop cards."

**When the user confirms, push each card in order:**

For each task, pipe the body via stdin:

```bash
bishop card add --lane "<Lane>" --title "<Title>" --tag "<Tag>" --description-file - --bottom << 'BODY'
### Why
<fill in>

### Acceptance
- <fill in>
BODY
```

After all cards are created, print a brief summary:

| Card | Title | Lane | Tag |
|------|-------|------|-----|
| <short ID from output> | ... | ... | ... |

Do NOT push automatically. Wait for the user to say "push".

---

**Closing card-action prompt.** After the summary table, if a **source card** was captured at the start (Path 1 above), ask the user what to do with it:

> Source card **#N** — \<title\> — is still in lane `<laneName>`. What now?
> (`close` / `done` / `leave`)

- `close` → `bishop card close <number>` (marks closed; if the card has a linked GitHub issue, the CLI closes that too).
- `done` → `bishop card move <number> --to-lane "Done" --to-position 0` (the CLI auto-closes on entry to the system `Done` lane).
- `leave` → no-op; the card stays where it is.

If there is no source card (Paths 2 and 3), skip this prompt entirely.

ARGUMENTS: $ARGUMENTS
