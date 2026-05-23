---
name: bish-onboard
description: Adopt Bishop in any project in one interview — detects git/workspace/skills/CLAUDE.md state and runs only the missing steps. Idempotent — re-running only performs missing steps. Use when the user wants to set up Bishop in this directory, mentions "onboard Bishop", or invokes `/bish-onboard`.
allowed-tools: Bash(bishop:*), Bash(git:*), Read, Write, Edit, Glob, Grep, AskUserQuestion
bishop.category: setup
---

**Goal:** Adopt Bishop in the current directory in one interview. Each step gates on detected state — if a step has already been done, report it and move on without prompting. Re-running this skill on an already-onboarded project is a no-op.

This skill ships with Bishop and installs alongside the other `bish-*` skills via `bishop install-skills`. Run it from a Claude Code session opened at the directory you want to onboard.

<what-to-do>

Work through the steps below in order. Keep a running list of outcomes — one short string per step — so you can print the summary in step 6 even if a later step is skipped.

---

### Step 1 — Git repository

Run `git rev-parse --is-inside-work-tree` (capture stdout, suppress stderr).

- If it prints `true` → record `git: already a git repo` and continue.
- Otherwise ask:

  > This directory is not a git repository. Run `git init` here?
  > (`y` to initialise / `n` to skip — Bishop will still track work, but you'll
  > lose card↔commit references and the GitHub-issue integration)

  - On `y` → run `git init`, record `git: initialised`.
  - On `n` → record `git: skipped (not a git repo)`.

---

### Step 2 — Bishop workspace

Run `bishop workspace current --json`.

- If it succeeds → parse the JSON and record `workspace: already registered as <name>`. Continue to step 3.
- If it exits non-zero → run `bishop workspace init` with no arguments. The CLI defaults to cwd, seeds the default lanes (`To Do`, `Doing`, `Done`) and the canonical tag set (`feature`, `bug`, `chore`, `docs`, `arch`, `test`, `spike`), and auto-detects the GitHub remote when one is configured. Then re-run `bishop workspace current --json` to capture the new workspace name. Record `workspace: registered as <name>` and append the GitHub link from the init output if one was detected.

If `bishop workspace init` fails, STOP and surface the stderr as-is. Do not proceed with later steps in a half-initialised state.

---

### Step 3 — Bish skills

Bishop bundles its `bish-*` skills with `bishop.exe`. They install to `~/.claude/skills/` via `bishop install-skills`.

1. Glob `~/.claude/skills/bish-*` (use `$env:USERPROFILE` on Windows / `$HOME` elsewhere) and record the set of installed skill directory names.
2. If `bish-onboard` itself is installed but any other `bish-*` skill expected by the current Bishop release is missing, run `bishop install-skills`.
   - You can determine which skills the current Bishop release ships by globbing for `skills/bish-*` next to `bishop.exe`, or simply by checking that `bish-arch`, `bish-audit-docs`, `bish-coverage`, `bish-grill-me`, `bish-tests`, `bish-work-on-card`, and `bish-onboard` are all present.
3. Record either `skills: installed (<N> skills)` or `skills: already up to date`.

`bishop install-skills` is idempotent (overwrites in place) — when uncertain, prefer running it over leaving the user with a partial install.

---

### Step 4 — CLAUDE.md pointer

Bishop's `WorkspaceContextSeeder` writes a one-line CLAUDE.md pointer to `.bishop/BISHOP_CONTEXT.md` on every workspace launch. If the user is onboarding from a fresh Claude Code session that was *not* launched via the Bishop UI, the seeder has not yet run.

1. Check whether `CLAUDE.md` exists at the workspace root and, if so, whether it already contains the string `.bishop/BISHOP_CONTEXT.md` (the pointer marker used by the seeder).
2. If the pointer is already present → record `CLAUDE.md: pointer already present` and continue.
3. Otherwise, prepare the change:
   - If `CLAUDE.md` does not exist, the file to write is:
     ```
     # <workspace-name>

     > See [.bishop/BISHOP_CONTEXT.md](./.bishop/BISHOP_CONTEXT.md) — Bishop CLI reference and live workspace state for LLM agents.
     ```
   - If `CLAUDE.md` exists but lacks the pointer, insert the pointer line (with a blank line above and below it) immediately after the first `# ` heading. If there is no `# ` heading, insert at the top of the file.
4. Show the user the proposed diff (the new file, or the inserted lines plus their immediate context) and ask:

   > Add the Bishop pointer to `CLAUDE.md`?
   > (`y` to write the change / `n` to skip)

   - On `y` → write the file using `Write` (new file) or `Edit` (insertion). Record `CLAUDE.md: pointer added`.
   - On `n` → record `CLAUDE.md: skipped`.

Do not reformat or rewrite anything else in `CLAUDE.md`. The marker check uses the literal string `.bishop/BISHOP_CONTEXT.md` so re-running the skill on an already-pointed file is a no-op.

---

### Step 5 — Summary

Print a single markdown block summarising what changed and what was skipped:

```
## Bishop onboarding complete

- git: <outcome>
- workspace: <outcome>
- skills: <outcome>
- CLAUDE.md: <outcome>

Next steps:
- Run `bishop` to open this workspace in the Bishop UI.
- Run `/bish-grill-me` to plan your next chunk of work and push cards to the board.
```

If any step was skipped or errored, report the user's reason rather than claiming success.

</what-to-do>

<guardrails>

- Do NOT overwrite an existing `CLAUDE.md` without showing the diff first. Do NOT overwrite one that already contains the marker `.bishop/BISHOP_CONTEXT.md` — the skill is meant to be idempotent on re-run.
- Do NOT call `bishop workspace init` with non-default flags unless the user asks. Defaults match the supported onboarding flow.
- Do NOT push cards to GitHub during onboarding. Onboarding only sets up local state; pushing is the user's call later.
- If any CLI step fails, STOP and surface the stderr as-is rather than improvising a recovery. The summary in step 6 should still be printed for whatever steps did complete.

</guardrails>
