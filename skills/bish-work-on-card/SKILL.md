---
name: bish-work-on-card
description: Fetches a Bishop card by Number (#N) from the current workspace, auto-moves it to "Doing", explores the codebase, implements the changes, then prompts before moving to "Done" and committing with a "(card #N)" reference. Use when the user wants to work on a specific Bishop card.
allowed-tools: Bash(bishop:*), Bash(dotnet:*), Bash(git:*), Read, Edit, Write, Glob, Grep, Agent
bishop.scope: card
bishop.command: /bish-work-on-card {{card_number}}
bishop.stage: false
bishop.category: execute
---

The context-pack below bundles workspace metadata, task data (card body), recent git history, and Bishop convention procedures (including shell tool selection) used by both Bishop CLI and Claude — canonical source: `.bishop/BISHOP_CONTEXT.md`.

---

Accepts an **optional** card Number (`bish-work-on-card 42` or `bish-work-on-card #42`).
If omitted, claims the top card from "To Do" and asks the user to confirm
before proceeding.

If the user passes multiple IDs or a range, STOP and tell them this skill
works on one card per session — long-running sessions accumulate context
from prior work, which hurts both cost and quality. Ask them to pick one
and start a fresh session for each subsequent card.

<what-to-do>

**Before anything else — load the context-pack:**

**Path A — user supplied a Number:**
```
bishop context-pack work-on-card --card <number>
```
If the command exits non-zero, surface the stderr message as-is and STOP.
Do NOT guess which card the user meant.

**Path B — no Number supplied:** claim the top of "To Do" first:
```
bishop card claim --json
```
If the command exits non-zero (empty source lane), STOP and surface the
stderr message as-is — do not invent a card.

Parse the claim JSON and ask the user to confirm:

> Claimed `#N` — '<title>' from [To Do]. Work on this card?
> (`y` to proceed / paste a different Number / `n` to skip)

- On `y` → proceed. Then call:
  ```
  bishop context-pack work-on-card --card <N>
  ```
- On a different Number → revert the claim first:
  ```
  bishop card move <claimed-number> --to-lane "To Do" --to-position 1
  ```
  Then call:
  ```
  bishop context-pack work-on-card --card <user-number>
  ```
- On `n` → revert the claim and STOP:
  ```
  bishop card move <claimed-number> --to-lane "To Do" --to-position 1
  ```

**Parse the context-pack JSON** (applies to both paths):
- `workspace.name` — echo back so the user can confirm the destination:
  > **Workspace:** <name>
- `skill_specific.card` — card metadata (`number`, `title`, `description`,
  `lane_name`, `tag`, `is_closed`). If this is `null`, the card was not found — STOP.
- `skill_specific.related_cards` — summaries of cards referenced in the source card's `### Related` section (empty array when none); each entry has `number`, `title`, `lane_name`, `is_closed`.
- `workspace.context_md` — project orientation text (used in step 2 below).
- `conventions` — STABLE procedure sections. Use `conventions["Shell selection"]`
  when choosing the Bash vs PowerShell tool throughout this run.
- `git.commits` — recent commit history.

Do NOT call `bishop card show` (or `view`) after the context-pack. `skill_specific.card.description` already contains the full card body verbatim; re-fetching wastes a turn.

**Closed-card guard:** If `is_closed` is `true`:
- If you are on Path B (the card was just claimed), revert the claim first:
  ```
  bishop card move <claimed-number> --to-lane "To Do" --to-position 1
  ```
- STOP and output this error:
  > Card #N is already closed — run `bishop card reopen <number>` first if
  > you want to work on it.

Do NOT echo the title, move the card to "Doing", or begin any implementation.

Echo the card title back on its own line so the user can confirm the right
card was loaded before any move or implementation:

> **Card #N:** <title>

1. **Auto-move the card to "Doing"** (no prompt — mirrors the work-on-issue
   contract of "start work without asking"):
   ```
   bishop card move <number> --to-lane "Doing" --to-position 0
   ```

   If the move fails (e.g. the workspace has no "Doing" lane), STOP and surface
   the error. Do not invent a substitute lane name.

   Skip this step if `lane_name` from the context-pack is already "Doing" (this
   includes the Path B happy-path, where `card claim` already moved the card).

2. Explore the codebase areas relevant to the card **via the Explore subagent**
   before writing any code.
   - Use the `Agent` tool with `subagent_type: "Explore"` and `model: "haiku"`.
   - Brief it with: the card title + description, relevant `workspace.context_md`
     sections from the context-pack, and the specific questions you need answered
     ("where is X defined", "which files would need to change to add Y", "what
     existing patterns handle Z").
   - Ask it to return file paths + line numbers + short excerpts, NOT to dump
     whole files. Keep large file contents out of the main context.
   - Only fall back to direct Read/Grep when the Explore agent's findings need
     a targeted follow-up on a specific file/line range you already know about.
   - Follow any dependency order or architectural conventions in `workspace.context_md`.
     Do not modify layers or modules the card does not require.

3. Implement the changes described in the card's description.
   - Match the coding style, naming conventions, and patterns already present.
   - Do not add libraries or introduce patterns not already in use.
   - Do not gold-plate — implement exactly what the card describes.

4. **Test coverage check** — before validating, analyse what was changed:
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

5. Validate the changes:
   - Run the build (`dotnet build`) and confirm it succeeds.
   - Run the tests (`dotnet test`) and confirm none are broken.
   - Run slopwatch, gated on availability and relevance:
     - **Availability gate:** if `.config/dotnet-tools.json` does not exist
       or does not contain a `slopwatch` package entry, skip slopwatch and
       print `slopwatch: skipped (not configured in .config/dotnet-tools.json)`,
       then continue to step 6.
     - **Relevance gate:** otherwise run `git diff --name-only HEAD`; if no
       path ends in `.cs`, skip slopwatch and print
       `slopwatch: skipped (no .cs changes)`, then continue to step 6.
     - Otherwise run `dotnet tool run slopwatch analyze --hook`:
     - **Exit 0** — no slop introduced; continue to step 6.
     - **Exit 2** — new violation introduced by this session. Surface the
       full slopwatch output, then **skip** steps 6–7 (the Agent-notes draft
       and commit-confirmation prompt). Instead, prompt the user:

       > Append findings to card #N? (`y`/`n`) [y]:

       On `y` (or empty input), append the verbatim slopwatch output as a
       `### Slopwatch findings (YYYY-MM-DD)` block (using today's date) via
       the temp-file procedure (see `Card Push Procedure` in
       `BishopContext.static.md` — `--append-description-file` follows the
       same write→push→remove flow):
       ```
       bishop card edit <number> --append-description-file ".bishop/tmp-card-slopwatch.md"
       ```
       Then STOP — leave the card in "Doing" and the working tree untouched.
       Prefer fixing the violation over suppressing it on a follow-up run;
       only suppress via `[SlopwatchSuppress("SW00x", "<20+ char reason>")]`
       when the flagged pattern is genuinely best-effort and the reason is
       accurate.
     - **Any other non-zero exit** (e.g. tool not found, config error) —
       hard failure: surface the error and STOP. Do not commit.

6. Output a concise completion summary:

   ## Done — Card #N: <title>
   **Files changed:**
   - `path/to/file` — what changed and why

   **Decisions made during implementation:**
   - <any non-obvious choices, with rationale>

   **Follow-up cards to consider:**
   - <anything discovered that is out of scope but worth tracking>

7. **Draft Agent notes**, then ask the user to confirm finalizing the card.

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

   Derive a pre-filled Conventional Commits proposal from the card's `tag`
   (from the context-pack) and title:

   Tag → prefix mapping:
   - `feature` → `feat`
   - `bug` → `fix`
   - `chore` → `chore`
   - `docs` → `docs`
   - `refactor` → `refactor`
   - `test` → `test`
   - no tag or unrecognised tag → `chore`

   Proposal format: `<prefix>: <title> (card #N)`

   Present the combined prompt — confirming implies: write Agent notes to the
   card, move it to Done, and commit:

   > Write findings to card #N, move to Done, and commit as `feat: Add lane CRUD (card #42)`?
   > (`y` to do all three / paste a different commit message to use instead / `n`
   > to leave the card in "Doing" and the working tree untouched)

   - On `y` (or an edited commit message) → run in order:
     1. Write the drafted `### Agent notes` block to
        `.bishop/tmp-card-agent-notes.md` via the temp-file procedure (see
        `Card Push Procedure` in `BishopContext.static.md`). Do not run
        `card edit` yet.
     2. Commit:
        ```
        git add -A && git commit -m "<message>"
        ```
        If the commit fails (e.g. pre-commit hook), do NOT run the card edit
        — leave the card in "Doing" with no description changes, surface
        the error, and let the user re-run the commit manually. The temp
        file may be left in place for retry.
     3. On a successful commit, capture the hash and branch — run each git
        command separately, capture the output, and pass the literal values
        to `card edit` (do not use shell variable expansion):
        ```
        git log -1 --format=%H
        git rev-parse --abbrev-ref HEAD
        ```
     4. Issue a single `card edit` that appends the notes, moves the card
        to Done, and records the commit hash + branch:
        ```
        bishop card edit <number> --append-description-file ".bishop/tmp-card-agent-notes.md" --to-lane "Done" --commit-hash <full-sha> --commit-branch <branch>
        ```
     5. `Remove-Item` the temp file via the `PowerShell` tool.
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
