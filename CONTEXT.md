# Bishop.AI — Project Context

This file documents what is *shipped today* — domain, tech stack, architecture, conventions. For where the project is headed and which features have been explicitly cut or deferred, see [DIRECTION.md](DIRECTION.md).

## Domain
A Windows desktop app for managing AI-assisted coding workflows. The user has many local code directories (workspaces); Bishop.AI is a single place to organise them, track work against each one via a per-workspace kanban board, and launch a Windows Terminal session (with `claude`) pointed at the chosen directory. Single-user, local-first. The name is a nod to Bishop, the synthetic science officer in *Aliens*.

## Tech Stack
- **UI:** WinUI 3 (Windows App SDK 1.6), unpackaged.
- **Architecture pattern:** MVVM via `CommunityToolkit.Mvvm` (source generators for `ObservableProperty` / `RelayCommand`).
- **App layer:** MediatR for commands/queries.
- **DI:** `Microsoft.Extensions.DependencyInjection` via the generic host.
- **Data:** EF Core 9 + SQLite (WAL mode for concurrent UI + CLI access). DB file at `%AppData%\Bishop.AI\bishop.db`.
- **Testing:** xUnit + FluentAssertions. Handlers and repos tested against in-memory or temp SQLite. No UI tests for MVP.
- **Target framework:** `net10.0` for Core / Data / App / Cli / Tests; `net10.0-windows10.0.19041.0` for Bishop.UI.

## Architecture

### Repository layout
- `src/` — .NET projects (Core, Data, App, Cli, UI).
- `tests/Bishop.Tests/` — xUnit project.
- `skills/` — vendored Claude Code skill files (`work-on-card-bishop`, `grill-me-bishop`) shipped with `bishop.exe` and installed to `~/.claude/skills/` via `bishop install-skills`.
- `installer/` — Wix v5 project that produces the per-user MSI. See `installer/README.md`.
- `notes/_archive/` — pre-grill design notes; preserved for decision rationale but superseded by DIRECTION.md.

### Layers
Layered, with strict one-way dependencies. Modify in this order when implementing a feature:

1. **Bishop.Core** — entities (Workspace, Lane, Card, Tag, CardTag), domain primitives, enums. No external deps.
2. **Bishop.Data** — `BishopDbContext`, EF Core configurations, migrations, repository interfaces + implementations. References Core.
3. **Bishop.App** — MediatR `IRequest` types and handlers, application services (e.g. the terminal launcher), validators. References Core + Data.
4. **Bishop.UI** and **Bishop.Cli** — presentation peers. UI is the WinUI 3 desktop app (Views, ViewModels, DI composition root). Cli is the `bishop` console executable. Both reference App; neither references Data directly.
5. **Bishop.Tests** — xUnit project under `tests/Bishop.Tests`. References whichever layers it tests.

Dependency direction: **Core → Data → App → { UI, Cli }**. UI and Cli go through MediatR handlers in App for everything.

### Kanban model
A workspace owns an ordered list of lanes; the three default lanes ("To Do", "Doing", "Done") are seeded on workspace creation. Cards belong to a single lane and carry an ordered position. Tags are workspace-scoped (with optional colour) and attach to cards via the `CardTag` join entity.

### CLI surface (`bishop`)
The `bishop` console executable is the primary integration surface for skills (e.g. `work-on-card-bishop`). Unversioned and additive-only — commands and flags are not renamed or removed once shipped.

- `bishop workspace list [--json]`
- `bishop workspace current [--json]` — resolves the workspace from the current working directory by ancestor match
- `bishop workspace init [--path <dir>] [--name <name>]` — register a directory (defaults to cwd) as a workspace and seed the default lanes; idempotent (no-op when fully seeded, fills gaps when partial)
- `bishop card add --lane <name> --title <text> [--description <text> | --description-file <path>] [--tag <name>...] [-w <workspace>]`
- `bishop card list [-w] [--json]`
- `bishop card view <card-id> [-w] [--json]`
- `bishop card move <card-id> --to-lane <name> --to-position <int> [-w]`
- `bishop card edit <card-id> [--title <t>] [--description <d> | --description-file <path>] [--tag <name>...] [--clear-tags] [-w]` — updates only the supplied fields; `--clear-tags` empties tags; `--tag` replaces all tags
- `bishop card claim [--lane <name>] [-w] [--json]` — picks the top card from the source lane (default "To Do"), moves it to "Doing", and emits its details; exits non-zero if the source lane is empty
- `bishop card remove <card-id> [-w]`
- `bishop lane list|add|rename|move|remove [-w]` — lane CRUD; `remove` refuses non-empty lanes
- `bishop tag list|add [--colour <hex>]|remove [-w]` — `add` accepts an optional `--colour` flag (6-char hex with or without `#`; defaults to `#888888`)
- `bishop install-skills` — copies the bundled skills under `skills/` to `%USERPROFILE%\.claude\skills\`. Run once on a fresh install; idempotent.

Card identifiers accept either a workspace-scoped Number (`42`, `#42`) or the first 8 hex chars of the GUID as a short-ID prefix. Number lookup is exact; hex-prefix lookup falls back and rejects ambiguous prefixes with a list of 8-char hex candidates on stderr. CLI output renders `#N` (e.g. `#42`) rather than the full UUID.

### Current UI scope
Bishop.UI is the interactive surface; the CLI remains the automation surface for skills. UI affordances by entity:

- **Workspaces:** left-hand list with drag-to-reorder; add-workspace dialog (create new folder or attach existing); per-workspace launch-terminal button (Windows Terminal + `claude` at the workspace path); per-workspace notes panel below the kanban (persisted text + drag-to-resize; expanded state stored per workspace).
- **Lanes:** add / rename / delete / reorder via inline board UI. `remove` refuses non-empty lanes, matching the CLI.
- **Cards:** view detail dialog; edit title/description/tags; delete; drag-and-drop between lanes and within a lane (writes position immediately).
- **Tags:** create / remove / recolour via the card edit dialog.
- **Skills:** launcher buttons on each card and on the workspace header — see [Skill integration](#skill-integration).
- **Theming:** dark theme applied across shell, nav, board chrome, and dialogs.

### Skill integration
Bundled Claude Code skills (`skills/work-on-card-bishop`, `skills/grill-me-bishop`) ship with `bishop.exe` and are installed to `%USERPROFILE%\.claude\skills\` via `bishop install-skills` (overwrites on each run). Each skill is a directory containing a `SKILL.md` whose YAML frontmatter declares:

- `name` — skill identifier (required).
- `description` — user-facing summary.
- `allowed-tools` — comma-separated Claude Code tool allowlist.
- `bishop.scope` — `card` (button on each card) or `workspace` (button on workspace header); null/missing → not surfaced in the UI.
- `bishop.command` — slash-command template launched on click. Placeholders: `{{card_number}}` (card scope) and `{{workspace_path}}` (workspace scope).

`DiscoverSkillsQueryHandler` (Bishop.App) scans `~/.claude/skills/` at workspace load and feeds two button groups in `WorkspaceDetailPage`. Clicking a skill renders the template, opens Windows Terminal at the workspace path, and runs `claude "<rendered command>"` (`LaunchSkillCommandHandler`; falls back to PowerShell if `wt.exe` is unavailable). Adding a new skill: drop a directory under `skills/`, set scope + command, rebuild, run `bishop install-skills`.

## Conventions
- **CLI is the automation surface; UI is the interactive surface.** Skills mutate state through `bishop` CLI invocations; humans use the UI directly for card / lane / tag edits. Both writers share the SQLite DB in WAL mode.
- **Naming:** standard .NET (PascalCase types/members, _camelCase private fields, `I`-prefix interfaces). One public type per file.
- **Async:** all I/O async; `Async` suffix; pass `CancellationToken` through handlers.
- **MVVM:** ViewModels derive from `ObservableObject`; commands use `[RelayCommand]`. No code-behind beyond constructor + `InitializeComponent`.
- **MediatR:** one request + one handler per file, colocated. Validators (if any) live next to the request.
- **EF Core:** entities are persistence-ignorant POCOs; configuration via `IEntityTypeConfiguration<T>` classes, not data annotations.
- **Migrations:** generated via `dotnet ef migrations add` from the Data project; checked in.
- **Tests:** Arrange/Act/Assert with blank lines between sections; `FluentAssertions` for all assertions.
- **Formatting:** `dotnet format` clean; `.editorconfig` at repo root.

## Out of scope
Recorded so future-me doesn't drift into them:
- **Cross-platform** (Mac / Linux). Windows-only by design.
- **Multi-user / cloud sync.** Single-user local app. No accounts, no servers.
- **GitHub Issues / Projects sync.** The kanban is standalone; the existing `grill-me` → `push-tasks` flow handles GitHub.
- **Plugin system.** Future tabs ship as in-tree code, not external plugins.
