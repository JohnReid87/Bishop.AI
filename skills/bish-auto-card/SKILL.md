---
name: bish-auto-card
description: Unattended sibling of /bish-work-on-card. Accepts a single card Number, implements the card, runs build + tests, commits, and moves the card to "Done" with --no-close. No prompts. Exits non-zero on any failure so a parent loop (bishop work-next) can react. Use when invoked by automation, not interactively.
allowed-tools: Bash(bishop:*), Bash(dotnet:*), Bash(git status:*), Bash(git add:*), Bash(git commit:*), Bash(git diff:*), Bash(git log:*), Read, Edit, Write, Glob, Grep, Agent
---

**Orientation:** if `BISHOP_CONTEXT.md` exists in the workspace root, read it first — it documents this workspace's lanes, tags, and the safe `bishop` CLI subcommands. Bishop regenerates it on every launch so the content is current.

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

**Before anything else — detect the active Bishop workspace:**

Run `bishop workspace current --json`.

- If the command exits non-zero or produces no output, exit non-zero with:

  > **Not in a Bishop workspace.** `bishop workspace current` failed.

- Otherwise, parse the JSON and echo the workspace name back so the parent
  log captures the destination:

  > **Workspace:** <name>

---

**For the card:**

1. **Fetch the card.** Run:
   ```
   bishop card view <number> --json
   ```
   If the command exits non-zero (no match), exit non-zero and surface the
   stderr message as-is. Do NOT guess which card the caller meant.

   Parse the JSON and capture:
   - `number` — the canonical `#N` reference (used in commit messages and
     headings, regardless of what the caller typed)
   - `title`, `description`, `laneName`, `tags`, `isClosed`

   **Closed-card guard:** If `isClosed` is `true`, exit non-zero with:

   > Card #N is already closed — run `bishop card reopen <number>` first if
   > you want to work on it.

   Do NOT move the card or begin any implementation.

   Echo the card title back on its own line so the parent log records which
   card was loaded:

   > **Card #N:** <title>

2. **Ensure the card is in "Doing".** The bishop loop normally claims the
   card before invoking this skill, so `laneName` should already be `Doing`.
   If it is not, move it now (no prompt):
   ```
   bishop card move <number> --to-lane "Doing" --to-position 0
   ```
   If the move fails (e.g. the workspace has no "Doing" lane), exit non-zero
   and surface the error. Do not invent a substitute lane name.

3. Read CONTEXT.md to orient yourself in the domain and solution structure.

4. Explore the codebase areas relevant to the card **via the Explore subagent**
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
   - **No prompt on ambiguity.** Where `/bish-work-on-card` would ask the user
     whether tests add value, default to "write tests" and proceed. The parent
     loop has no interactive surface, and writing tests is the safer default.
   - If the card is already tagged `test`, or only test files changed, skip
     this check entirely.

7. Validate the changes. **Any non-zero exit here aborts the skill — no commit,
   no Done move, card stays in "Doing".**
   - Run `dotnet build`. If it exits non-zero, run `dotnet clean` once, then
     re-run `dotnet build`. If the retry still fails, exit non-zero and surface
     the build error. Do **not** improvise any `Remove-Item` or manual file
     deletion to clear the cache — the `dotnet clean` path is the only
     permitted cleanup.
   - Run `dotnet test`. If any test fails, exit non-zero and surface the
     failure. Do not retry `dotnet test` — test failures are real signals.

8. **Derive the commit message.** Use the same tag → prefix mapping as
   `/bish-work-on-card`:

   - `feature` or `enhancement` → `feat`
   - `bug` → `fix`
   - `chore` → `chore`
   - `docs` → `docs`
   - `refactor` → `refactor`
   - `test` → `test`
   - no tag or unrecognised tag → `chore`

   Take the **first** tag from `tags` (the same field captured in step 1).
   Format: `<prefix>: <title> (card N)`.

9. **Commit and move to Done, in that order.** No prompt.
   ```
   git add -A
   git commit -m "<derived message>"
   ```
   If `git commit` exits non-zero (e.g. pre-commit hook, nothing to commit,
   signing failure), exit non-zero immediately. Do NOT move the card to
   "Done"; leave it in "Doing" and let the staged changes (if any) sit in
   the working tree for the user to inspect.

   On a successful commit:
   ```
   bishop card move <number> --to-lane "Done" --to-position 0 --no-close
   ```
   The `--no-close` flag (card #72) leaves the card with `IsClosed=false` so
   a human can review before closing.

   If the `bishop card move` exits non-zero, exit non-zero — the commit has
   already landed and the user can move the card manually after inspecting.

10. **Never push.** Pushing is out of scope. The parent loop or the user
    decides when to push after review.

11. On full success, output a concise completion line so the parent log has
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
- **Card stays in "Doing" on failure.** Only on a clean build + tests + commit
  does the card move to "Done". The `--no-close` flag preserves the human
  review gate.
- **No multi-card mode.** Exactly one Number per invocation. Exit non-zero if
  zero or more than one is supplied.
- **No claim path.** This skill never claims a card. If the supplied card is
  not in "Doing", move it there (step 2) — but never pull from "To Do".
- If the card is tagged `bug`, reproduce the symptom before fixing.
- If the card is tagged `feature` or `enhancement`, check whether any existing
  code partially addresses it before writing new code.
- If blocked by a missing dependency (e.g. card X requires card Y), exit
  non-zero with a clear message rather than improvising.
- If `bishop card move` fails because the lane doesn't exist, surface the
  error — do not pick an alternative lane name.
- **No improvised file cleanup.** Do not run `Remove-Item`, `rm`, `del`, or
  any ad-hoc file/directory deletion to clear build artefacts. The only
  permitted cache-clear path is `dotnet clean` as documented in step 7.

</guardrails>
