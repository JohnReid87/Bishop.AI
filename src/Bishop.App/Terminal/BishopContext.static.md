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
- `bishop card claim [--lane <name>] [--json]` — pop the top card of a lane into "Doing"
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
