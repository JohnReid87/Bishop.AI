---
name: work-on-card-bishop
description: Fetches a Bishop card by short-ID prefix from the current workspace, auto-moves it to "Doing", explores the codebase, implements the changes, then prompts before moving to "Done" and committing with a "(card <short-id>)" reference. Use when the user wants to work on a specific Bishop card.
allowed-tools: Bash(bishop:*), Bash(dotnet:*), Bash(git:*), Read, Edit, Write, Glob, Grep, Agent
---

Accepts a single card short-ID prefix (`work-on-card-bishop 1a2b3c4d`).

If the user passes multiple IDs or a range, STOP and tell them this skill
works on one card per session — long-running sessions accumulate context
from prior work, which hurts both cost and quality. Ask them to pick one
and start a fresh session for each subsequent card.

<what-to-do>

**Before anything else — detect the active Bishop workspace:**

Run `bishop workspace current --json`.

- If the command exits non-zero or produces no output, STOP and tell the user:

  > **Not in a Bishop workspace.** Run `bishop workspace list` to see available
  > workspaces, then `cd` into one of the listed paths and retry.

- Otherwise, parse the JSON and echo the workspace name back so the user can
  confirm the destination before any further work begins:

  > **Workspace:** <name>

---

**For the card:**

1. Fetch the card via:
   ```
   bishop card view <short-id> --json
   ```

   If the command exits non-zero (no match, or ambiguous prefix), STOP and
   surface the stderr message as-is. Do NOT guess which card the user meant
   — the resolver lists all candidates on stderr; relay them and ask the user
   to disambiguate with a longer prefix.

   Parse the JSON and capture:
   - `id` — extract the first 8 hex chars for the canonical short-ID (used in
     the commit message and headings, regardless of what prefix the user typed)
   - `title`, `description`, `laneName`, `tags`

   Echo the card title back on its own line so the user can confirm the right
   card was loaded before any move or implementation:

   > **Card <short-id>:** <title>

2. **Auto-move the card to "Doing"** (no prompt — mirrors the work-on-issue
   contract of "start work without asking"):
   ```
   bishop card move <short-id> --to-lane "Doing" --to-position 0
   ```

   If the move fails (e.g. the workspace has no "Doing" lane), STOP and surface
   the error. Do not invent a substitute lane name.

   Skip this step if `laneName` from step 1 is already "Doing".

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

6. Validate the changes:
   - Run the build (`dotnet build`) and confirm it succeeds.
   - Run the tests (`dotnet test`) and confirm none are broken.

7. Output a concise completion summary:

   ## Done — Card <short-id>: <title>
   **Files changed:**
   - `path/to/file` — what changed and why

   **Decisions made during implementation:**
   - <any non-obvious choices, with rationale>

   **Follow-up cards to consider:**
   - <anything discovered that is out of scope but worth tracking>

8. Ask the user whether to **move the card to "Done"**:

   > Move card <short-id> to "Done"? (y/n)

   If yes, run:
   ```
   bishop card move <short-id> --to-lane "Done" --to-position 0
   ```

   If no, leave the card in "Doing".

9. Offer to commit and push the changes. Derive a pre-filled Conventional
   Commits proposal from the card's first tag (captured in step 1) and title:

   Tag → prefix mapping:
   - `feature` or `enhancement` → `feat`
   - `bug` → `fix`
   - `chore` → `chore`
   - `docs` → `docs`
   - `refactor` → `refactor`
   - `test` → `test`
   - no tag or unrecognised tag → `chore`

   Proposal format: `<prefix>: <title> (card <short-id>)`

   Example — tags `["feature"]`, title "Add lane CRUD":
   > Proposed: `feat: Add lane CRUD (card 1a2b3c4d)` — confirm, edit, or skip?

   Present the proposal and ask the user to confirm, provide their own message,
   or skip entirely. Do not stage, commit, or push without explicit confirmation.
   If the user declines, leave the working tree as-is.

   Use the canonical 8-char short-ID from step 1, not whatever prefix the user
   typed.

</what-to-do>

<guardrails>

- Do NOT move the card to "Done" automatically — the user reviews first.
- Do NOT accept multiple card IDs — one card per session, full stop.
- Do NOT start work on dependent cards unless explicitly asked.
- If the card is tagged `bug`, reproduce the symptom first before fixing.
- If the card is tagged `feature` or `enhancement`, check whether any existing
  code partially addresses it before writing new code.
- If blocked by a missing dependency (e.g. card X requires card Y to be done
  first), stop and tell the user rather than improvising.
- If `bishop card move` fails because the lane doesn't exist, surface the error
  — do not pick an alternative lane name.

</guardrails>
