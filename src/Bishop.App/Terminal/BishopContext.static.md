## Workflow

Bishop ships with a family of Claude Code skills (`bish-*`) that collaborate
through the kanban board. Each skill plays one role; pick the right one for
the task instead of doing everything inside a single session.

### Planning skills — produce cards

- `bish-grill-me` — relentlessly interviews the user about a plan or design,
  then pushes the agreed-on tasks as cards on the board. Use when work is
  **not yet on the board** — you have an idea and need it stress-tested and
  broken down into trackable items.
- `bish-arch` — architectural / SOLID review of the current .NET solution.
  Walks findings one at a time; agreed items become cards tagged `arch`.
- `bish-coverage` — runs the coverage script, identifies classes below the
  line threshold, and pushes test-gap cards tagged `test`.
- `bish-tests` — audits the quality of existing tests (shallow asserts,
  brittle mocks, missing edge cases, untested public methods) and pushes
  cards tagged `test`.
- `bish-audit-docs` — audits Markdown docs in the repo for drift against the
  code and edits the docs in place per agreed finding.

### Execution skills — consume cards

- `bish-work-on-card` — interactive. Accepts a single card Number
  (e.g. `42` or `#42`), moves it to "Doing", implements it, then prompts
  before moving it to "Done" and committing. **One card per session** —
  long-running sessions accumulate context that hurts cost and quality.
  Use when work is **already a card** and you want it implemented now.
- `bish-auto-card` — unattended sibling of `bish-work-on-card`, intended
  for automation (e.g. a parent loop driving `bishop card claim`). Same
  contract, but no prompts and non-zero exit on any failure.

### Choosing between `bish-grill-me` and `bish-work-on-card`

- No Number yet, just an idea or proposal → `bish-grill-me`. Produces cards.
- A Number in hand (`#42`) → `bish-work-on-card`. Consumes one card.

## Publishing cards to GitHub

Cards live in the local SQLite DB by default and are **not** synced to
GitHub automatically. To surface a card as a GitHub issue (e.g. for
stakeholders or external collaborators), run:

```
bishop card push <number>
```

Requires the workspace to be linked (`bishop workspace set-github <owner/repo>`)
and the `gh` CLI to be authenticated. Once pushed, the card stores its issue
number; subsequent `bishop card close`, `bishop card reopen`, and moves into
or out of the `Done` lane also close / reopen the linked issue.

Push is **on-demand** — call it explicitly for the cards that need to be
visible on GitHub; everything else stays local.

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

## Card model

Bishop tracks work as **cards** inside **lanes** on a per-workspace kanban board.
Cards are addressed by their workspace-scoped Number, written as `#N` (e.g. `#42`).
Card identifiers also accept the first 8 hex characters of the card's GUID.
Tags are workspace-scoped and attach to cards via the `CardTag` join.

### Card body convention

Bodies use H3-section markdown. Required sections: `### Why` and `### Acceptance`.
Optional sections (include only when relevant): `### Changes`, `### Decided`,
`### Out of scope`, `### Related`.

Use backticks for code-like tokens: commands, file paths, flags, and identifiers.

Pass multi-line bodies via `--description-file -` (stdin) — do not escape `\n` inline.

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
- `bishop workspace set-github <owner/repo>` — link the workspace to a GitHub repo

### Card

- `bishop card list [--json]`
- `bishop card view <id> [--json]`
- `bishop card add --lane <name> --title <text> [--description <text> | --description-file <path>] [--tag <name>...]`
- `bishop card move <id> --to-lane <name> --to-position <int> [--no-close]` — `--no-close` keeps the card open when moving into `Done`
- `bishop card edit <id> [--title <t>] [--description <d> | --description-file <path>] [--tag <name>...] [--clear-tags]`
- `bishop card claim [--lane <name>] [--tag <name>] [--json]` — pop the top card of a lane into "Doing"; with `--tag`, picks the first card carrying that tag
- `bishop card push <id>` — create a GitHub issue for the card
- `bishop card close <id>` / `bishop card reopen <id>`

### Lane

- `bishop lane list`
- `bishop lane add` / `bishop lane rename` / `bishop lane move`

### Tag

- `bishop tag list`
- `bishop tag add [--colour <hex>]`

Prefer `--json` output for any command an agent will parse. Pipe multi-line
descriptions via `--description-file -` (stdin) to avoid quote escaping.
