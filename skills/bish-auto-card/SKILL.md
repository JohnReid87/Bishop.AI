---
name: bish-auto-card
description: Unattended sibling of /bish-work-on-card. Accepts a single card Number, implements the card, runs build + tests, commits, and moves the card to "Done" with --no-close. No prompts. Exits non-zero on any failure so a parent loop (bishop work-next) can react. Use when invoked by automation, not interactively.
allowed-tools: Bash(bishop:*), Bash(dotnet:*), Bash(git status:*), Bash(git add:*), Bash(git commit:*), Bash(git diff:*), Bash(git log:*), Bash(git rev-parse:*), Read, Edit, Write, Glob, Grep, Agent
bishop.category: execute
---

Shell tool selection (Bash vs PowerShell) — follow [bishop context print --section "Shell selection"](.bishop/BISHOP_CONTEXT.md#shell-selection-stable) (STABLE).

---

Accepts **exactly one** card Number (`bish-auto-card 42` or `bish-auto-card #42`).
This is the unattended sibling of `/bish-work-on-card`, designed to be spawned
by `bishop work-next` once per claimed card. The bishop loop has already
claimed the card and moved it to "Doing" before invoking this skill.

If the user (or caller) passes zero or multiple IDs, exit non-zero with a
short message — there is no claim fallback and no multi-card mode.

**No prompts. Ever.** This skill is invoked via `claude -p` in a non-interactive
session. Any branch that would normally ask the user a question must instead
take the documented default and proceed, or exit non-zero if no safe default
exists.

<what-to-do>

**Pre-supplied context block**

The initial message that invoked this skill includes a `<bishop-context>` JSON
block pre-assembled by `bishop work-next`. Read it now:

- `workspace.path` — set this as `$WORKSPACE_PATH` (the workspace boundary
  enforced throughout this run)
- `workspace.name`, `workspace.lanes`, `workspace.tags` — workspace bootstrap data
- `card` — the claimed card's metadata (number, title, description, laneName, tag, isClosed)
- `git.recentCommits` — the 20 most recent commits (equivalent to `git log --oneline -20`)
- `relatedCards` — summaries of cards referenced in the card's `### Related` section

Do **not** run `bishop skill bootstrap`, `bishop card view`, `git log`, or
related-card lookups — this data is already here.

Echo the workspace name and card title from the context block so the parent
log records the destination:

> **Workspace:** <workspace.name>
> **Card #N:** <card.title>

---

**Workspace-path boundary — enforced throughout this run**

Every `Edit`, `Write`, and `NotebookEdit` call must target an absolute path
that starts with `$WORKSPACE_PATH` (the value of `workspace.path` from the
context block above). Before issuing any file-edit tool call, verify the
target path resolves under the workspace root. Any path outside the workspace
is a violation — see the pre-commit audit (step 6) for the exact remediation
sequence.

---

**For the card:**

1. Read CONTEXT.md to orient yourself in the domain and solution structure.

2. Explore the codebase areas relevant to the card **via the Explore subagent**
   before writing any code.
   - Use the `Agent` tool with `subagent_type: "Explore"`.
   - The `Agent` tool's own `description` parameter is a **3-to-5-word task
     label** — it identifies the agent invocation in logs (e.g. `"Explore test
     patterns"`, `"Find card handler"`). It is **not** the card description.
     The card title + description belongs in `prompt`. Example:
     ```
     Agent(
       subagent_type="Explore",
       description="Explore card handler files",
       prompt="Card: <title>\n<description>\n\nQuestions: where is X defined, ..."
     )
     ```
   - Brief the prompt with: the card title + description, relevant CONTEXT.md
     sections, and the specific questions you need answered ("where is X
     defined", "which files would need to change to add Y", "what existing
     patterns handle Z").
   - Ask it to return file paths + line numbers + short excerpts, NOT to dump
     whole files. Keep large file contents out of the main context.
   - Only fall back to direct Read/Grep when the Explore agent's findings need
     a targeted follow-up on a specific file/line range you already know about.
   - Follow any dependency order or architectural conventions in CONTEXT.md.
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
   - **No prompt on ambiguity.** Where `/bish-work-on-card` would ask the user
     whether tests add value, default to "write tests" and proceed. The parent
     loop has no interactive surface, and writing tests is the safer default.
   - If the card is already tagged `test`, or only test files changed, skip
     this check entirely.

5. Validate the changes. **Any non-zero exit here aborts the skill — no commit,
   no Done move, card stays in "Doing".**
   - Run `dotnet build`. If it exits non-zero, run `dotnet clean` once, then
     re-run `dotnet build`. If the retry still fails, exit non-zero and surface
     the build error. Do **not** improvise any `Remove-Item` or manual file
     deletion to clear the cache — the `dotnet clean` path is the only
     permitted cleanup.
   - Run `dotnet test`. If any test fails, exit non-zero and surface the
     failure. Do not retry `dotnet test` — test failures are real signals.

6. **Pre-commit workspace-path audit.** Before committing, review every
   `Edit`, `Write`, and `NotebookEdit` tool call made during this session
   and collect the absolute target path of each.

   For each path, normalize it (resolve to an absolute path) and verify it
   starts with `$WORKSPACE_PATH`.

   **Clean run** (all paths under `$WORKSPACE_PATH`): proceed to step 7.

   **Violation** (any path is outside `$WORKSPACE_PATH`): take these steps in
   order and then exit non-zero:
   1. Print the offending absolute paths to chat.
   2. Revert all in-workspace changes:
      ```
      git restore .
      git clean -fd
      ```
      (This removes both tracked-file modifications and any untracked files
      auto-card wrote inside the workspace.)
   3. Append a note to the card's description via `bishop card edit`:
      ```
      bishop card edit <number> --append-description-file -
      ```
      Pipe the following block as stdin (substituting real values):
      ```
      ### Out-of-workspace edits (needs inspection)
      Auto-card aborted; the following files outside `<$WORKSPACE_PATH>` were
      modified and could not be reverted automatically:
      - `<absolute path 1>`
      - `<absolute path 2>`
      ```
   4. Move the card back to "To Do":
      ```
      bishop card move <number> --to-lane "To Do" --to-position 0
      ```
   5. Exit non-zero. Do NOT commit. Do NOT move the card to Done.

7. **Draft Agent notes and derive the commit message.** No prompt.

   Silently compose an `### Agent notes` block from the session context.
   Use this template, **omitting any section that has no relevant content** —
   do not emit empty headers:

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

   Then derive the commit message. Use the same tag → prefix mapping as
   `/bish-work-on-card`:

   - `feature` → `feat`
   - `bug` → `fix`
   - `chore` → `chore`
   - `docs` → `docs`
   - `refactor` → `refactor`
   - `test` → `test`
   - no tag or unrecognised tag → `chore`

   Take the tag from `card.tag` in the pre-supplied context block.
   Format: `<prefix>: <title> (card N)`.

8. **Commit and update the card, in that order.** No prompt.
   ```
   git add -A
   git commit -m "<derived message>"
   ```
   If `git commit` exits non-zero (e.g. pre-commit hook, nothing to commit,
   signing failure), exit non-zero immediately. Do NOT update the card;
   leave it in "Doing" and let the staged changes (if any) sit in
   the working tree for the user to inspect.

   On a successful commit, capture the hash and branch then record them —
   run each git command separately, capture the output, and pass the literal
   values to `set-commit` (do not use shell variable expansion):
   ```
   git log -1 --format=%H
   git rev-parse --abbrev-ref HEAD
   bishop card set-commit <number> --hash <full-sha> --branch <branch>
   ```
   If `set-commit` exits non-zero, continue anyway — it is non-fatal; the
   commit has already landed.

   Then pipe the drafted `### Agent notes` block to
   `card edit` via stdin:
   ```
   bishop card edit <number> --append-description-file - --to-lane "Done" --no-close
   ```
   (Pipe the notes block as stdin — use a shell heredoc or equivalent.)
   `--no-close` keeps `IsClosed=false` so the human review gate is preserved.

   If `bishop card edit` exits non-zero, exit non-zero — the commit has
   already landed and the user can update/move the card manually after
   inspecting.

9. **Never push.** Pushing is out of scope. The parent loop or the user
    decides when to push after review.

10. On full success, output a concise completion line so the parent log has
    a clear marker:

    > **Done — Card #N:** <title> — committed as `<prefix>: <title> (card N)`,
    > moved to "Done" (still open).

</what-to-do>

<guardrails>

- **No prompts, ever.** Any place `/bish-work-on-card` would ask the user a
  question, take the documented default or exit non-zero. The caller is a
  non-interactive `claude -p` session spawned by `bishop work-next`.
- **Non-zero exit on any deviation.** Failed build, failed test, failed commit,
  closed-card guard, missing lane, missing card — all exit non-zero. The
  parent loop stops on non-zero exits per the `bishop work-next` contract.
- **Card stays in "Doing" on failure.** Only on a clean build + tests + path
  audit + commit does the card move to "Done". Exception: a workspace-boundary
  violation (step 6) moves the card back to "To Do" so the inspection note is
  visible. The `--no-close` flag preserves the human review gate on success.
- **No multi-card mode.** Exactly one Number per invocation. Exit non-zero if
  zero or more than one is supplied.
- **No claim path.** This skill never claims a card. The `bishop work-next`
  loop always claims and moves the card to "Doing" before invoking the skill.
- If the card is tagged `bug`, reproduce the symptom before fixing.
- If the card is tagged `feature`, check whether any existing
  code partially addresses it before writing new code.
- If blocked by a missing dependency (e.g. card X requires card Y), exit
  non-zero with a clear message rather than improvising.
- If `bishop card move` fails because the lane doesn't exist, surface the
  error — do not pick an alternative lane name.
- **No improvised file cleanup.** Do not run `Remove-Item`, `rm`, `del`, or
  any ad-hoc file/directory deletion to clear build artefacts. The only
  permitted cache-clear path is `dotnet clean` as documented in step 5.
- **Workspace-path boundary.** Every `Edit`, `Write`, and `NotebookEdit` call
  must target a path under `$WORKSPACE_PATH`. Any out-of-workspace edit
  triggers the step 6 remediation: revert in-workspace changes, append an
  inspection note to the card, move the card back to "To Do", exit non-zero.

</guardrails>
