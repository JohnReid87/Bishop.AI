---
name: bish-work-on-card
description: Fetches a Bishop card by Number (#N) from the current workspace, auto-moves it to "Doing", explores the codebase, implements the changes, then prompts before moving to "Done" and committing with a "(card N)" reference. Use when the user wants to work on a specific Bishop card.
allowed-tools: Bash(bishop:*), Bash(dotnet:*), Bash(git:*), Read, Edit, Write, Glob, Grep, Agent
bishop.scope: card
bishop.command: /bish-work-on-card {{card_number}}
bishop.stage: false
bishop.category: execute
---

**Orientation:** if `.bishop/BISHOP_CONTEXT.md` exists in the workspace, read it first — it documents this workspace's lanes, tags, and the safe `bishop` CLI subcommands. Bishop regenerates it on every launch so the content is current.

Shell tool selection (Bash vs PowerShell) — follow `## Shell selection (STABLE)` in BISHOP_CONTEXT.md.

---

Accepts an **optional** card Number (`bish-work-on-card 42` or `bish-work-on-card #42`).
If omitted, claims the top card from "To Do" and asks the user to confirm
before proceeding.

If the user passes multiple IDs or a range, STOP and tell them this skill
works on one card per session — long-running sessions accumulate context
from prior work, which hurts both cost and quality. Ask them to pick one
and start a fresh session for each subsequent card.

<what-to-do>

**Before anything else — initialize from `bishop skill bootstrap`:**

Run `bishop skill bootstrap`. If it exits non-zero, surface the stderr line
verbatim to the user and STOP — the helper already explains the remediation.
On success, echo the workspace name back so the user can confirm the
destination before any further work begins:

> **Workspace:** <name>

---

**For the card:**

1. **Fetch the card.** Two paths depending on what the user supplied.

   **Path A — user supplied a Number:**
   ```
   bishop card view <number> --json
   ```
   If the command exits non-zero (no match), STOP and surface the stderr
   message as-is. Do NOT guess which card the user meant.

   **Path B — no Number supplied:** claim the top of "To Do":
   ```
   bishop card claim --json
   ```
   If the command exits non-zero (empty source lane), STOP and surface the
   stderr message as-is — do not invent a card.

   Parse the JSON and ask the user to confirm:

   > Claimed `#N` — '<title>' from [To Do]. Work on this card?
   > (`y` to proceed / paste a different Number / `n` to skip)

   - On `y` → proceed. The card is already in "Doing", so step 2 is a no-op.
   - On a different Number → revert the claim, then restart this step
     using their Number (Path A):
     ```
     bishop card move <claimed-number> --to-lane "To Do" --to-position 1
     ```
   - On `n` → revert the claim and STOP:
     ```
     bishop card move <claimed-number> --to-lane "To Do" --to-position 1
     ```

   Parse the JSON of the final chosen card and capture:
   - `number` — the canonical `#N` reference (used in commit messages and
     headings, regardless of what the user typed)
   - `title`, `description`, `laneName`, `tags`, `isClosed`

   **Closed-card guard:** If `isClosed` is `true`:
   - If you are on Path B (the card was just claimed), revert the claim first:
     ```
     bishop card move <claimed-number> --to-lane "To Do" --to-position 1
     ```
   - STOP and output this error (exit non-zero):
     > Card #N is already closed — run `bishop card reopen <number>` first if
     > you want to work on it.

   Do NOT echo the title, move the card to "Doing", or begin any implementation.

   Echo the card title back on its own line so the user can confirm the right
   card was loaded before any move or implementation:

   > **Card #N:** <title>

2. **Auto-move the card to "Doing"** (no prompt — mirrors the work-on-issue
   contract of "start work without asking"):
   ```
   bishop card move <number> --to-lane "Doing" --to-position 0
   ```

   If the move fails (e.g. the workspace has no "Doing" lane), STOP and surface
   the error. Do not invent a substitute lane name.

   Skip this step if `laneName` from step 1 is already "Doing" (this includes
   the Path B happy-path, where `card claim` already moved the card).

3. Read CONTEXT.md to orient yourself in the domain and solution structure.

4. Explore the codebase areas relevant to the card **via the Explore subagent**
   before writing any code.
   - Use the `Agent` tool with `subagent_type: "Explore"`.
   - Brief it with: the card title + description, relevant CONTEXT.md sections,
     and the specific questions you need answered ("where is X defined", "which
     files would need to change to add Y", "what existing patterns handle Z").
   - Ask it to return file paths + line numbers + short excerpts, NOT to dump
     whole files. Keep large file contents out of the main context.
   - Only fall back to direct Read/Grep when the Explore agent's findings need
     a targeted follow-up on a specific file/line range you already know about.
   - Follow any dependency order or architectural conventions in CONTEXT.md.
     Do not modify layers or modules the card does not require.

5. Implement the changes described in the card's description.
   - Match the coding style, naming conventions, and patterns already present.
   - Do not add libraries or introduce patterns not already in use.
   - Do not gold-plate — implement exactly what the card describes.

6. **Test coverage check** — before validating, analyse what was changed:
   - Identify any files added or modified in production projects (paths that do
     NOT contain `.Tests` or `Tests` in a directory segment).
   - If production files were touched, assess whether the new or changed code
     contains testable logic: command/query handlers, services, domain methods,
     non-trivial algorithms, or any logic with branching behaviour.
   - If testable logic is present and no test files were written in this session,
     **write the unit tests as part of this card** — do not defer them. Follow
     the existing test project structure and naming conventions.
   - Only ask the user if the need for tests is genuinely ambiguous (e.g. a
     thin pass-through or pure scaffolding with no meaningful behaviour). In
     that case, briefly describe what was added and ask:

     > The new code appears to be [description]. Tests may not add much value
     > here — should I write them anyway, or track as a follow-up?
     > (`write` / `follow-up` / `skip`)

   - If the card is already tagged `test`, or only test files changed, skip
     this check entirely.

7. Validate the changes:
   - Run the build (`dotnet build`) and confirm it succeeds.
   - Run the tests (`dotnet test`) and confirm none are broken.

8. Output a concise completion summary:

   ## Done — Card #N: <title>
   **Files changed:**
   - `path/to/file` — what changed and why

   **Decisions made during implementation:**
   - <any non-obvious choices, with rationale>

   **Follow-up cards to consider:**
   - <anything discovered that is out of scope but worth tracking>

9. **Draft Agent notes**, then ask the user to confirm finalizing the card.

   Before the prompt, silently compose an `### Agent notes` block from the
   session context. Use this template, **omitting any section that has no
   relevant content** — do not emit empty headers:

   ````markdown
   ### Agent notes

   #### Summary
   <1–3 sentences>

   #### Changes
   - <file or change>

   #### Decisions
   - <choice over alt — why>

   #### Tests
   - <what ran, what was added>
   ````

   Derive a pre-filled Conventional Commits proposal from the card's first tag
   (captured in step 1) and title:

   Tag → prefix mapping:
   - `feature` → `feat`
   - `bug` → `fix`
   - `chore` → `chore`
   - `docs` → `docs`
   - `refactor` → `refactor`
   - `test` → `test`
   - no tag or unrecognised tag → `chore`

   Proposal format: `<prefix>: <title> (card N)`

   Present the combined prompt — confirming implies: write Agent notes to the
   card, move it to Done, and commit:

   > Write findings to card #N, move to Done, and commit as `feat: Add lane CRUD (card 42)`?
   > (`y` to do all three / paste a different commit message to use instead / `n`
   > to leave the card in "Doing" and the working tree untouched)

   - On `y` (or an edited commit message) → run in order:
     1. Pipe the drafted `### Agent notes` block to `card edit` via stdin:
        ```
        bishop card edit <number> --append-description-file - --to-lane "Done"
        ```
        (Pipe the notes block as stdin — use a shell heredoc or equivalent.)
     2. Commit:
        ```
        git add -A && git commit -m "<message>"
        ```
     If the commit fails (e.g. pre-commit hook), do NOT roll the card back —
     surface the error and let the user re-run the commit manually.
   - On `n` → leave the card in "Doing" and the working tree as-is. Do not
     commit or move.

   Do not push. Pushing is out of scope for this skill.

</what-to-do>

<guardrails>

- Do NOT move the card to "Done" automatically — the user reviews first.
- Do NOT accept multiple card IDs — one card per session, full stop.
- Do NOT start work on dependent cards unless explicitly asked.
- If the card is tagged `bug`, reproduce the symptom first before fixing.
- If the card is tagged `feature`, check whether any existing
  code partially addresses it before writing new code.
- If blocked by a missing dependency (e.g. card X requires card Y to be done
  first), stop and tell the user rather than improvising.
- If `bishop card move` fails because the lane doesn't exist, surface the error
  — do not pick an alternative lane name.

</guardrails>
