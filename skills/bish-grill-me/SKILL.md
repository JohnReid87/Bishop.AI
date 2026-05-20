---
name: bish-grill-me
description: Interview the user relentlessly about a plan or design targeting the current Bishop workspace, then push the agreed cards directly to the board after confirmation. Use when working inside a Bishop workspace and the user wants to stress-test a plan, get grilled on their design, or mentions "grill me".
allowed-tools: Read, Glob, Grep, Write, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: workspace
bishop.command: /bish-grill-me
bishop.stage: true
---

**Orientation:** if `BISHOP_CONTEXT.md` exists in the workspace root, read it first — it documents this workspace's lanes, tags, and the safe `bishop` CLI subcommands. Bishop regenerates it on every launch so the content is current.

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

**Body format.** Single line; use literal `\n` to denote paragraph breaks. Template:

`<what, 1 sentence>.\nDecided: <choice> over <alt> because <reason>.\nAcceptance: <criteria>.`

Rules:
- Omit the `Decided:` clause if no real tradeoff was discussed — don't invent one.
- Do not restate the title in the body.
- Constraints, gotchas, or links go on their own `\n`-separated line.

After printing the task list, ask:
> "Please review the tasks above. Say **push** to create the Bishop cards."

**When the user confirms, push each card in order:**

For each task, run:

```
bishop card add --lane "<Lane>" --title "<Title>" --tag "<Tag>" --description "<body with \n replaced by real newlines>" --bottom
```

Replace literal `\n` sequences in the body with real newlines before passing
`--description`. If the body contains double-quotes, use `--description-file -`
and pipe the body via stdin instead.

After all cards are created, print a brief summary:

| Card | Title | Lane | Tag |
|------|-------|------|-----|
| <short ID from output> | ... | ... | ... |

Do NOT push automatically. Wait for the user to say "push".
