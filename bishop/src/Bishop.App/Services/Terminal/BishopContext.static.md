## Workflow

Bishop ships with a family of Claude Code skills (`bish-*`) that collaborate
through the kanban board. Each skill plays one role; pick the right one for
the task instead of doing everything inside a single session.

### Code skills — analyse production C# and produce cards (`bishop.category: code`)

- `bish-arch` — architectural / SOLID review of the current .NET solution.
  Walks findings one at a time; agreed items become cards tagged `arch`.
- `bish-dead-code` — dead-code review of the current .NET solution. Hunts
  unreferenced C# types/members, MediatR requests never dispatched, and DI
  registrations never injected. Walks findings one at a time; agreed items
  become cards tagged `chore`.
- `bish-security` — security audit of the current .NET solution. Scans for
  injection, weak crypto and hardcoded secrets, unsafe deserialization,
  missing authn/authz, plus stack-conditional checks and GitHub Actions
  workflow misconfig. Also runs `dotnet list package --vulnerable`. Walks
  findings one at a time; agreed items become cards tagged `security`.

### Tests skills — analyse the test surface and produce cards (`bishop.category: tests`)

- `bish-coverage` — runs the coverage script, identifies classes below the
  line threshold, and pushes test-gap cards tagged `test`.
- `bish-tests` — audits the quality of existing tests (shallow asserts,
  brittle mocks, missing edge cases, untested public methods) and pushes
  cards tagged `test`.

### Review skills — analyse other artefacts and produce cards (`bishop.category: review`)

- `bish-audit-docs` — audits Markdown docs in the repo for drift against the
  code and edits the docs in place per agreed finding.
- `bish-triage` — interrogates a free-text bug description, validates the
  suspected cause against the repo via the Explore subagent, and pushes a
  structured `bug` card (or a `spike` + fix-stub pair when root cause is
  unconfirmed).

### Conversational skills — explore and plan (`bishop.category: discuss`)

- `bish-grill-cards` — relentlessly interviews the user about a plan or design,
  then pushes the agreed-on tasks as cards on the board. Use when work is
  **not yet on the board** — you have an idea and need it stress-tested and
  broken down into trackable items.
- `bish-grill-docs` — relentlessly interviews the user about a doc that needs
  writing — purpose, audience, structure — then writes the markdown file
  in-session at the agreed path. Sibling of `bish-grill-cards` for when the
  follow-up to a grill is a document rather than code. Never pushes cards.
- `bish-scripts` — interviews the user about what they want to automate, then
  drafts and saves a PowerShell `.ps1` to `%AppData%\Bishop.AI\scripts\` so
  it appears immediately in the ScriptsPage launcher. Writes new scripts only;
  does not edit existing ones. No card is created.
- `bish-spec-cards` — document-seeded interrogation. Accepts a path to a
  feature spec markdown file (or a card Number), reads it, and grills the user
  about gaps, edge cases, and missing acceptance criteria before pushing
  implementation cards. Use when you **have a written spec** and want it
  stress-tested and broken into trackable items.

### Setup-Execute skills — onboard, configure, and implement cards (`bishop.category: setup` / `execute`)

Deterministic procedures that mutate state (filesystem, board, git). The
soul is the procedure itself — the agent reads steps in order.

- `bish-auto-card` — unattended sibling of `bish-work-on-card`, intended
  for automation (e.g. a parent loop driving `bishop card claim`). Same
  contract, but no prompts and non-zero exit on any failure.
- `bish-onboard` — adopt Bishop in any project in one interview. Detects
  git/workspace/skills/CLAUDE.md state and runs only the missing steps.
  Idempotent — re-running only performs missing steps.
- `bish-work-on-card` — interactive. Accepts a single card Number
  (e.g. `42` or `#42`), moves it to "Doing", implements it, then prompts
  before moving it to "Done" and committing. **One card per session** —
  long-running sessions accumulate context that hurts cost and quality.
  Use when work is **already a card** and you want it implemented now.
- `bish-life-init` — bootstraps `%APPDATA%/Bishop/life/bishop.life.json`
  with the bishop.life v1 schema (six seeded areas, empty inbox and
  stand-ups). Refuses to overwrite an existing file. Operates on the
  bishop.life data file, not a Bishop workspace.
- `bish-life-standup` — the daily bishop.life stand-up ritual: context
  pack, thread-by-thread walk, then one atomic rewrite of
  `bishop.life.json`. Operates on the bishop.life data file, not a
  Bishop workspace.
- `bish-life-add` — short-form bishop.life inbox capture; appends
  `InboxItem` entries for later triage in the stand-up. Operates on the
  bishop.life data file, not a Bishop workspace.

### Bishop-level / meta skills — operate on the skill family itself (`bishop.category: meta`)

These skills do not target a workspace's code; they operate on `skills/`
in the Bishop.AI repository. See [`docs/SKILL_FAMILY.md`](../docs/SKILL_FAMILY.md)
for the category rationale.

- `bish-write-skill` — authors a new Bishop skill. Interviews to pick a
  category (Conversational / Code / Tests / Review / Setup-Execute /
  Bishop-level), emits a skeleton `SKILL.md` to `skills/<name>/`, and
  checks the result against the family's canonical patterns.
- `bish-audit-skills` — audits the skill family against the canonical
  patterns in `docs/SKILL_FAMILY.md` (category, leading content,
  workspace-detection, STABLE-section references, heuristic content,
  frontmatter), walks findings with the user, and pushes refactor cards.

### Choosing between `bish-spec-cards`, `bish-grill-cards`, and `bish-work-on-card`

- You have a written spec → `bish-spec-cards`. Probes the document for gaps, produces cards.
- No written spec, just an idea → `bish-grill-cards`. Stress-tests the design, produces cards.
- A Number in hand (`#42`) → `bish-work-on-card`. Consumes one card.

## Commit-reference convention

When a commit implements work tracked by a card, end the Conventional Commits
subject with `(card #N)` so the card and the commit can be traced to each
other:

```
feat: Add lane CRUD (card #42)
fix: Skip closed cards in claim (card #58)
chore: Tidy board header spacing (card #114)
```

The `bish-work-on-card` skill proposes this format automatically. When
committing by hand, follow the same convention so future agents can locate
the work that produced a change.

## Auto-card permission contract

`bish-auto-card` runs unattended with `--permission-mode bypassPermissions`
(passed by `ClaudeCliRunner` via `bishop batch run`) so the loop never stalls
on per-tool approval prompts. Containment comes from deny rules and a path
hook, not an allowlist — see DIRECTION.md ("Automated Claude runs use
bypassPermissions") for the threat-model rationale. This section documents
the boundaries so you can audit the loop without source-diving.

### Containment layers

- `BISHOP_AUTO_CARD=1` activates `bishop hook check-path` (`PreToolUse` on
  `Edit` / `Write` / `NotebookEdit`), which blocks file writes outside the
  workspace root.
- The `deny` list in `.claude/settings.json` is still honoured under
  `bypassPermissions` — deny rules apply even when prompts are bypassed.
- The host (`RunBatchCommandHandler`) performs the final `git commit` itself;
  the skill cannot push.

### Denied (`.claude/settings.json` deny list)

The following are refused outright if the unattended loop attempts them:

- `git push` — no remote pushes; pushing is out of scope for automated loops
- `gh:*` — no GitHub CLI calls (issue creation, PR management, etc.)
- `curl` / `wget` — no network fetches
- `sudo`, `chmod`, `icacls` — no privilege or ACL changes
- `npm install`, `dotnet tool install` — no dependency installation
- `kill`, `taskkill`, `shutdown` — no process or machine control

## Card model

Bishop tracks work as **cards** inside **lanes** on a per-workspace kanban board.
Cards are addressed by their workspace-scoped Number, written as `#N` (e.g. `#42`).
Tags are a fixed global set of 8 names (`arch`, `bug`, `chore`, `docs`, `feature`, `security`, `spike`, `test`); a card holds at most one tag, stored as a nullable `TagName` string on the `Cards` table.

### Card body convention

Bodies use H3-section markdown. Required sections: `### Why` and `### Acceptance`.
Optional sections (include only when relevant): `### Changes`, `### Decided`,
`### Out of scope`, `### Related`.

Use backticks for code-like tokens: commands, file paths, flags, and identifiers.

Pass multi-line bodies via `--description-file <path>` pointing at a temp file under `.bishop/` — see the Card Push Procedure section for the full write→push→remove flow.

```
### Why
Describe the motivation in 1–3 sentences.

### Changes
- What to do, as bullet points.

### Acceptance
- Verifiable criterion one.
```

## CLI quick reference

All commands accept `-w <workspace>` to target a specific workspace; without it
they resolve from the current working directory.

### Workspace

- `bishop workspace list [--json]`
- `bishop workspace current [--json]` — resolves the workspace from cwd by ancestor match
- `bishop workspace init [--path <dir>] [--name <name>]` — register a directory; idempotent
- `bishop workspace remove [-w <name>] [--yes] [--dry-run]` — soft-delete; card data preserved, deletes `.bishop/` if present; `-w` allows removal by name after the directory is gone
- `bishop workspace record-skill-run --skill <name> --sha <sha>` — record that a review skill ran on the current workspace

### Card

- `bishop card list [--json]`
- `bishop card show <id> [--json]`
- `bishop card create --lane <name> --title <text> [--description <text> | --description-file <path>] [--tag <name>] [--bottom]`
- `bishop card move <id> --to-lane <name> --to-position <int>` — moves the card; moving into `Done` also closes it
- `bishop card edit <id> [--title <t>] [--description <d> | --description-file <path> | --append-description-file <path>] [--tag <name>] [--to-lane <name>] [--commit-hash <sha>] [--commit-branch <name>]` — pass `--tag ""` to clear the tag
- `bishop card claim [--lane <name>] [--tag <name>] [--json]` — pop the top card of a lane into "Doing"; with `--tag`, picks the first card carrying that tag
- `bishop card close <id>` / `bishop card reopen <id>`
- `bishop card set-commit <id> --hash <sha> --branch <name>` — record the commit that implemented a card

### Batch

- `bishop batch create --name <text> [--branch <name>] [--base <branch>] [--cards <n,...>] [--tag <name>] [--lane <name>]` — create a batch and provision a git worktree
- `bishop batch edit <name> --new-name <text>` — rename a batch
- `bishop batch list [--json]`
- `bishop batch show <name> [--json]`
- `bishop batch add-card <name> <card-id>` — assign a card to a batch
- `bishop batch remove-card <name> <card-id>` — unassign a card from a batch
- `bishop batch run <name> [--resume] [--model <model-id>]` — run a batch end-to-end via `bish-auto-card`; stops on card failure; `--resume` continues from the next undone card
- `bishop batch merge <name>` — merge the batch branch into the base branch with `--no-ff`
- `bishop batch clean-up <name>` — remove worktree, delete branch, close the batch, and close any Done-lane cards assigned to it (requires merge first); outputs `Closed card #N` for each card closed
- `bishop batch abandon <name>` — abandon a batch and remove its worktree
- `bishop batch prune` — remove worktrees for completed or abandoned batches
- `bishop batch remove <name>` — delete the batch record from the database

### Lane

- `bishop lane list` — lanes are fixed (Backlog / To Do / Doing / Done); no
  user-mutable lane CRUD

### Tag

- `bishop tag list`

### Findings

- `bishop findings record --skill <name> --sha <sha> --file <path> [--project <name>]` — persist a review skill's findings JSON; see `## Findings Recording Procedure` below for the full contract

### Context

- `bishop context print [--section <name>]` — print the workspace CONTEXT.md file, or a single named H2 section (ad-hoc / debug use; skills get these sections via the context pack)
- `bishop context-pack <skill-name> [--card <n>] [--list]` — emit a pre-stuffed JSON context bundle (workspace + git + skill-specific data + conventions)
- `bishop context-pack life-standup` — emit the bishop.life stand-up context pack (reads `bishop.life.json`, not the workspace DB)

### Skill

- `bishop skill bootstrap [--json]`

Returns a single JSON object containing:

| Field | Type | Description |
|---|---|---|
| `workspaceName` | string | Canonical name of the current workspace |
| `tags[].name` | string | Each workspace-scoped tag name |
| `lanes[].name` | string | Each lane name, in board order |

Skills call `bootstrap` as their mandatory first step rather than chaining
`workspace current` + `tag list` + `lane list` because it is a single
round-trip that returns everything an agent needs to initialise correctly —
workspace name, the full tag set, and the lane list — in one command.

**Error contract:** a non-zero exit means the workspace is not ready (no
workspace registered at the current path, or the Bishop database is
unavailable). Surface the stderr output verbatim and stop — the message
already explains the remediation.

Prefer `--json` output for any command an agent will parse. Pass multi-line
descriptions via `--description-file <path>` pointing at a temp file under
`.bishop/` (see the Card Push Procedure section).

## Skill conventions

The sections below are shared scaffolding that skills under `skills/`
reference instead of restating. Each header carries an inline label:

- **STABLE** — behaviour contract. Skills depend on the exact wording.
  Do not paraphrase, reorder, or skip steps when applying the section.
  Edits require auditing every skill that points at it.
- **TUNABLE** — voice / copy guidance. Paraphrase to fit context as
  needed; edits do not require a family-wide audit.

For the reasoning behind extracting these sections (and the boundary
between what belongs here versus inside each skill), see
[`docs/SKILL_FAMILY.md`](../docs/SKILL_FAMILY.md).

## Shell selection (STABLE)

When running shell commands, pick the tool that matches the command syntax:

1. **Use the PowerShell tool** if the command line contains PowerShell cmdlets —
   `ConvertFrom-Json`, `Where-Object`, `Select-Object`, `Get-ChildItem`,
   `Format-Table`, `Sort-Object`, `Measure-Object`, or any `Verb-Noun` cmdlet.
   PowerShell is the native shell on this host; mixing PowerShell cmdlets into a
   Bash call produces `command not found` (exit 127) errors.

2. **Use the Bash tool** if the command line contains POSIX utilities — `grep`,
   `sed`, `awk`, `xargs`, `wc`, `jq`, etc. For `bishop … --json` output that
   needs filtering or projection, prefer Bash + `jq` over piping into PowerShell
   cmdlets.

The `bishop` CLI itself is available in both tools; the rule governs the
**surrounding pipeline**, not the `bishop` call itself.

## Card Push Procedure (STABLE)

Push a card from a skill in three steps: write the body to a temp file
under `.bishop/`, push it with `--description-file <path>`, then delete
the temp file. Piping the body inline through stdin / heredoc was
unreliable (quote escaping, shell expansion, agents falling back to
temp files anyway) so the temp-file path is the house style.

1. **Write** the body with the `Write` tool to
   `.bishop/tmp-card-<slug>.md`. The body is plain markdown — no
   escaping, no heredoc quoting.

2. **Push** with `--description-file` pointing at the temp file, and
   `--bottom` so cards land in agreed order rather than reverse-stacked
   at the top of the lane:

   ```
   bishop card create --lane "<lane>" --title "<title>" --tag <tag> --description-file ".bishop/tmp-card-<slug>.md" --bottom
   ```

3. **Remove** the temp file with the **`PowerShell` tool**, not Bash:

   ```powershell
   Remove-Item ".bishop/tmp-card-<slug>.md"
   ```

   `Remove-Item` is a PowerShell cmdlet, so invoking it via the Bash
   tool produces `command not found` (exit 127). The cleanup step
   must use the `PowerShell` tool.

- `<lane>` is normally `"To Do"`; `bish-grill-cards` allows `"Backlog"`
  when the user asks for a parking spot.
- `<tag>` is a single tag name from the fixed set (`arch`, `bug`,
  `chore`, `docs`, `feature`, `security`, `spike`, `test`).
  Cards carry at most one tag.
- `<slug>` is a short kebab-case hint of the card (e.g.
  `tmp-card-abandon-open.md`) so multiple cards in one session don't
  collide.
- Skills that need extra body sections (`### Risk`, `### Repro`,
  `### Issues`, `### Related`, …) add them between `### Why` and
  `### Acceptance` following the H3 convention in `## Card model`.
- The card body convention itself (required / optional sections) is
  documented in `## Card model > ### Card body convention` above.

## Task List Preview Format (STABLE)

When a skill has gathered multiple cards and is about to push them,
print a preview that the user confirms or edits *before* any
`bishop card create` runs. Use H3 headings for each card, a single
`Tag` / `Lane` metadata line, H4 subsections for the body, and a
`---` separator between cards.

```markdown
> **Target workspace:** <name>

### 1. <concise card title>
**Tag:** <tag>  ·  **Lane:** <lane>

#### Why
<body>

#### Acceptance
- <criterion>

---

### 2. <concise card title>
**Tag:** <tag>  ·  **Lane:** <lane>

#### Why
<body>

#### Acceptance
- <criterion>
```

- Numbering restarts at `1` per preview.
- Use the same H4 subsections that will appear in the pushed card
  body (`#### Why`, `#### Acceptance`, plus any optional sections
  the card needs). The H4 → H3 demotion happens on push.
- Cards push in the order they appear in the preview; the first
  card lands at the bottom of `<lane>` and subsequent cards stack
  below it, preserving order.

## Source Card Closing Prompt (STABLE)

When a skill was invoked against a *source* card (e.g. a `bish-grill-cards`
or `bish-triage` run that turned the source card's idea into child
cards), prompt the user about the source card's fate after the
children have been pushed. Use exactly this prompt and option set:

```
> Source card **#N** — <title> — is still in lane `<laneName>`. What now?
> (`close` / `done` / `leave`)
```

CLI mapping for each option:

- `close` → `bishop card close <number>` — marks the card closed.
- `done` → `bishop card move <number> --to-lane "Done" --to-position 0`
  — moving into the system `Done` lane auto-closes the card.
- `leave` → no-op; the card stays in its current lane.

Only offer this prompt when the skill actually consumed a source
card. Skills that produce cards from a free-form interview with no
source card (e.g. `bish-grill-cards` on a fresh idea) skip the prompt.

## Card Granularity Rules (TUNABLE)

Use these heuristics when deciding whether a candidate task should
be one card, folded into another, or split.

- One card ≈ one PR's worth of work.
- Fold one-line or sub-30-minute changes into the nearest related
  card rather than filing them standalone.
- Merge cards that touch the same file or module for the same
  reason.
- Split only when the pieces have independent acceptance criteria,
  or when they could ship in separate PRs without one blocking the
  other.

## Findings Recording Procedure (STABLE)

Review skills (`bish-arch`, `bish-security`, `bish-tests`, `bish-coverage`,
`bish-audit-docs`, `bish-dead-code`) record their findings as the **final step** of a completed
run via `bishop findings record`. "Completed run" means the skill ran its full
review and the user was walked through every finding — including runs with no
findings, or where all findings were already carded. Do NOT call it on error
STOPs (failed `bishop skill bootstrap`, missing workspace, etc.). The command
writes `.bishop/findings/<skill>.json` and `.bishop/findings/<skill>.html`; the
Monitoring tab reads these for `Last run` / `Commits since`.

### Track findings during triage

While walking findings one at a time (see `Per-finding Walk Pattern`), keep a
session log (in memory) with one entry per surfaced finding:

- `title` — the finding's one-sentence headline.
- `body` — multi-line description: location(s), what, why-it-matters,
  suggested-action.
- `severity` — the finding's `high` / `med` / `low` (or `critical`).
- `location` — `file:line` (or comma-separated locations).
- `file` — primary source file the finding is about (path relative to the
  workspace, e.g. `bishop/src/Bishop.App/Foo.cs`). Part of the finding identity hash
  so reruns can match prior findings — must be stable across runs for the
  same underlying issue.
- `rule` — the dimension / category / rule-id the finding belongs to
  (e.g. `SOLID/SRP`, `CWE-89`, `mutation-coverage`, `Unreferenced type`).
  Part of the identity hash.
- `symbol` — the canonical identifier the finding is about — the type,
  member, request, registration, or test target (e.g. `OrderService`,
  `OrderService.Submit`). Part of the identity hash.
- **pending outcome** — derived from the user's triage choice:
  - **Card it (new)** → `pending-card:<session-index>` (resolved to
    `carded:#<N>` once the card is pushed and its Number is known).
  - **Cluster with #N** → reuse the `pending-card:<session-index>` assigned to
    the card it was clustered into.
  - **Dismiss — context** → `dismissed`.
  - **Defer** → `parked`.

Every finding the skill surfaced must appear in the log. After cards are pushed,
resolve each `pending-card:<n>` marker to `carded:#<N>` using the returned card
Numbers, so every entry carries one of the three final outcomes
(`carded:#<N>` / `dismissed` / `parked`).

### Record the run

Write the findings JSON to `.bishop/findings/<skill-name>.json` using the Write
tool (use the actual kebab-case skill name). Because the Write tool is not a
shell command, `$` signs and backticks inside finding bodies pass through
unchanged — no quoting gymnastics needed.

```json
{
  "findings": [
    {
      "title": "<short title>",
      "body": "<full body: locations, what, why-it-matters, suggested-action>",
      "outcome": "carded:#<N>",
      "severity": "high",
      "location": "<file:line[, file:line]>",
      "file": "<src/.../File.cs>",
      "rule": "<dimension or rule-id>",
      "symbol": "<TypeName or TypeName.Member>"
    }
  ]
}
```

Then capture HEAD and record, passing the file written above:

```bash
# Capture the current SHA.
git rev-parse HEAD

# Record, substituting the literal SHA value and the kebab-case skill name.
bishop findings record --skill <skill-name> --sha <captured-sha> --file .bishop/findings/<skill-name>.json
```

For project-scoped skills (currently `bish-tests`), add `--project <name>` so
the run is keyed on `(workspace, skill, project)` instead of overwriting prior
runs of the same skill against a different project:

```bash
bishop findings record --skill bish-tests --sha <captured-sha> --project <ProjectName> --file .bishop/findings/bish-tests.json
```

`<ProjectName>` should be the `.csproj` name (e.g. `Bishop.App`), matching what
the staging dialog's project picker emits. Solution-scoped skills (`bish-arch`,
`bish-security`, `bish-dead-code`, `bish-coverage`, `bish-audit-docs`) omit
`--project`.

A successful invocation prints `Recorded N finding(s) for '<skill-name>'` plus
the JSON / HTML paths under `.bishop/findings/`. On non-zero exit, surface the
error to the user but do not abort — the review is complete whether or not the
record write succeeds.

### No-findings path

When the skill surfaces no findings (all dimensions clean, nothing to triage),
write an empty-array payload and record it:

```json
{
  "findings": []
}
```

```bash
git rev-parse HEAD

bishop findings record --skill <skill-name> --sha <captured-sha> --file .bishop/findings/<skill-name>.json
```

Then congratulate the user and STOP without pushing anything.

### Findings JSON schema

The `bishop findings record --file <path>` command reads JSON of the shape above; the
validator (`Bishop.App.Findings.FindingsValidator`) rejects malformed input.

Field rules:

- `title` (required) — non-empty string. One-sentence finding headline.
- `body` (required) — non-empty string. Use `\n` for newlines. Should contain
  location(s), what's wrong, why it matters, and the suggested action.
- `outcome` (required) — exactly one of:
  - `dismissed` — user explained why this isn't an issue.
  - `parked` — user wants to revisit later.
  - `carded:#<n>` — became a Bishop card (the literal `#` is required; `<n>` is
    the workspace-scoped card Number).
- `severity` (optional) — `high` / `med` / `low` / `critical`. Drives the HTML
  chip colour. `null` or absent is allowed.
- `location` (optional) — `file:line` or comma-separated locations for findings
  spanning multiple sites.
- `file` (required for stable identity) — primary source file path relative to
  the workspace. Combined with `rule` and `symbol` to compute the finding's
  identity hash (`sha1(skillName + projectName + file + rule + symbol)`), which
  lets reruns match prior findings. If any of `file`, `rule`, `symbol` is
  omitted the handler falls back to a title-based identity — emit all three.
- `rule` (required for stable identity) — the dimension / category / rule-id
  the finding belongs to (e.g. `SOLID/SRP`, `CWE-89`, `mutation-coverage`,
  `Unreferenced type`). Should be a stable label that doesn't change between
  runs for the same underlying issue.
- `symbol` (required for stable identity) — the canonical identifier the
  finding is about (type, member, request, registration, or test target —
  e.g. `OrderService`, `OrderService.Submit`).

The top-level `projectName` (optional) scopes the run to a specific project
within the solution. Equivalent to passing `--project <name>` on the CLI, which
overrides any value already present in the JSON. Used by `bish-tests` so that
running it against `Bishop.App` then `Bishop.UI` keeps both finding sets
instead of overwriting the first.

An empty `findings: []` array is valid and is the right shape for the
no-findings run path.

## Per-finding Walk Pattern (TUNABLE)

Review skills (`bish-arch`, `bish-security`, `bish-tests`,
`bish-coverage`, `bish-audit-docs`, `bish-dead-code`) walk findings one at a time
rather than batching. For each finding:

1. Print the full body — location(s), what, why-it-matters,
   suggested action, plus your own recommended verdict (e.g.
   "Recommended: card it — this is a clear DIP violation worth
   fixing before the next API host is added").

2. Use `AskUserQuestion` with these options (the recommended one
   first, suffixed `" (Recommended)"`):

   - **Card it (new)** — file this finding as a new card using the
     skill's body template.
   - **Cluster with #N: \<title\>** — fold this finding into a card
     already agreed earlier in the session. Append its location and
     a one-line summary to that card's `### Related` section. Only
     offer when at least one prior in-session card exists.
   - **Dismiss — context** — user explains why this isn't an issue
     in this codebase.
   - **Defer** — note it but don't card it now.

3. Wait for the user's choice before moving on. Do not present a
   batched list of findings; the walk is one-at-a-time so the user
   can steer mid-stream.
