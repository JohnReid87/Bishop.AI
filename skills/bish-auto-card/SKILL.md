---
name: bish-auto-card
description: Unattended skill invoked by `bishop batch run`. Accepts a single card Number, implements the card, runs build + tests, then writes .bishop/handoff.json for the host to commit and move to Done. No prompts. Exits non-zero on any failure.
allowed-tools: Bash(bishop:*), Bash(dotnet:*), Bash(git status:*), Bash(git diff:*), Read, Edit, Write, Glob, Grep, Agent
bishop.category: execute
---

Shell tool selection (Bash vs PowerShell) — follow [bishop context print --section "Shell selection"](.bishop/BISHOP_CONTEXT.md#shell-selection-stable) (STABLE).

---

Accepts **exactly one** card Number (`bish-auto-card 42` or `bish-auto-card #42`).
This skill is invoked via `claude -p` by `bishop batch run` (`RunBatchCommandHandler`).
The batch handler loops over its cards sequentially, moves each card to "Doing"
via `UpdateCardCommand` before invoking this skill, then reads
`.bishop/handoff.json` after the skill exits with code 0 to commit, record the
hash and branch, append notes, and move the card to Done. The caller has already
moved the card to "Doing" before this skill runs.

If the user (or caller) passes zero or multiple IDs, exit non-zero with a
short message — there is no claim fallback and no multi-card mode.

**No prompts. Ever.** This skill is invoked via `claude -p` in a non-interactive
session. Any branch that would normally ask the user a question must instead
take the documented default and proceed, or exit non-zero if no safe default
exists.

<what-to-do>

**Pre-supplied context block**

The initial message that invoked this skill includes a `<bishop-context>` JSON
block pre-assembled by the calling automation. Read it now:

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

6. **Draft Agent notes and derive the commit message.** No prompt.

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

   Then derive the commit message bullets — one short string per meaningful
   change. Use the same tag → prefix mapping as `/bish-work-on-card`:

   - `feature` → `feat`
   - `bug` → `fix`
   - `chore` → `chore`
   - `docs` → `docs`
   - `refactor` → `refactor`
   - `test` → `test`
   - no tag or unrecognised tag → `chore`

   Take the tag from `card.tag` in the pre-supplied context block. The host
   composes the final commit message as `<prefix>: <title> (card N)` plus the
   bullets as the body — you supply the bullets, not the subject line.

7. **Write `.bishop/handoff.json` as your final action.** No prompt. This is
   the signal that the skill completed cleanly. The host reads this file after
   `claude -p` exits with code 0, commits, updates the card, and moves it to
   Done. Exiting without writing the file is treated as failure by the host.

   Write the file using the `Write` tool with this exact schema:

   ```json
   {
     "commit_body_bullets": ["<one short string per change>"],
     "touched_files": ["<relative path of each file changed>"],
     "notes": "<the full ### Agent notes block as a string, or null>"
   }
   ```

   - `commit_body_bullets` — the bullet strings for the git commit body. Each
     string should be concise (one line), e.g. `"Remove work-next CLI command"`.
     Do NOT include leading `- ` dashes; the host formats the body.
   - `touched_files` — every file path written or edited during this session,
     relative to `$WORKSPACE_PATH`.
   - `notes` — the full `### Agent notes` markdown block (multi-line string),
     or `null` if there is nothing meaningful to append.

   Target path: `<workspace.path>/.bishop/handoff.json`.

   Do NOT run `bishop card edit`, `git add`, `git commit`, or
   `bishop card set-commit` — the host performs all of those in-process.

8. **Never push.** Pushing is out of scope. The parent loop or the user
   decides when to push after review.

9. On full success (handoff.json written), output a concise completion line
   so the parent log has a clear marker:

    > **Done — Card #N:** <title> — handoff written; host will commit and move
    > to Done.

</what-to-do>

<guardrails>

- **No prompts, ever.** Any place `/bish-work-on-card` would ask the user a
  question, take the documented default or exit non-zero. The caller is a
  non-interactive `claude -p` session.
- **Non-zero exit on any deviation.** Failed build, failed test, closed-card
  guard, missing lane, missing card — all exit non-zero. The caller stops on
  non-zero exits.
- **Card stays in "Doing" on failure.** Only on a clean build + tests +
  handoff.json write does the host move the card to "Done". The `KeepOpen`
  flag preserves the human review gate on success.
- **No multi-card mode.** Exactly one Number per invocation. Exit non-zero if
  zero or more than one is supplied.
- **No claim path.** This skill never claims a card. `bishop batch run` moves
  each card to "Doing" via `UpdateCardCommand` before invoking this skill.
- **No direct git or card commit/move calls.** Do NOT run `git add`, `git
  commit`, `bishop card set-commit`, or `bishop card edit --to-lane Done`. The
  host performs all of those after reading handoff.json.
- If the card is tagged `bug`, reproduce the symptom before fixing.
- If the card is tagged `feature`, check whether any existing
  code partially addresses it before writing new code.
- If blocked by a missing dependency (e.g. card X requires card Y), exit
  non-zero with a clear message rather than improvising.
- **No improvised file cleanup.** Do not run `Remove-Item`, `rm`, `del`, or
  any ad-hoc file/directory deletion to clear build artefacts. The only
  permitted cache-clear path is `dotnet clean` as documented in step 5.
- **Workspace-path boundary.** Every `Edit`, `Write`, and `NotebookEdit` call
  must target a path under `$WORKSPACE_PATH`. A pre-commit hook enforces this
  boundary — do not attempt to bypass it.

</guardrails>
