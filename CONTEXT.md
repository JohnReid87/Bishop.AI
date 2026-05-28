# Bishop.AI — Project Context

> See [.bishop/BISHOP_CONTEXT.md](./.bishop/BISHOP_CONTEXT.md) — Bishop CLI reference and live workspace state for LLM agents.


This file documents what is *shipped today* — domain, tech stack, architecture, conventions. For where the project is headed and which features have been explicitly cut or deferred, see [DIRECTION.md](DIRECTION.md).

## Domain
A Windows desktop app for managing AI-assisted coding workflows. The user has many local code directories (workspaces); Bishop.AI is a single place to organise them, track work against each one via a per-workspace kanban board, and launch a Windows Terminal session (with `claude`) pointed at the chosen directory. Single-user, local-first. The name is a nod to Bishop, the synthetic science officer in *Aliens*.

## Tech Stack
- **UI:** WinUI 3 (Windows App SDK 1.6), unpackaged.
- **Architecture pattern:** MVVM via `CommunityToolkit.Mvvm` (source generators for `ObservableProperty` / `RelayCommand`).
- **App layer:** MediatR for commands/queries.
- **DI:** `Microsoft.Extensions.DependencyInjection` via the generic host.
- **Data:** EF Core 9 + SQLite (WAL mode for concurrent UI + CLI access). DB file at `%AppData%\Bishop.AI\bishop.db` (override with the `BISHOP_DB` env var — set it to an absolute path; useful for tests and portable configs).
- **Testing:** xUnit + FluentAssertions. Handlers and repos in `Bishop.App` are tested against in-memory or temp SQLite; ViewModels in `Bishop.ViewModels` are testable against the `IUiDispatcher` abstraction. No tests target `net10.0-windows`; visual rendering and `xaml.cs` orchestration are out of scope.
- **Target framework:** `net10.0` for Core / Data / App / ViewModels / Cli / Tests; `net10.0-windows10.0.19041.0` for Bishop.UI.

## Architecture

### Repository layout
- `src/` — .NET projects (Core, Data, App, ViewModels, Cli, UI, Game).
- `tests/Bishop.Tests/` — xUnit project.
- `skills/` — vendored Claude Code skill files shipped with `bishop.exe` and installed to `~/.claude/skills/` via `bishop install-skills`. Grouped into four categories (see [docs/SKILL_FAMILY.md](docs/SKILL_FAMILY.md) for rationale):
  - **Review:** `bish-arch`, `bish-dead-code`, `bish-audit-docs`, `bish-coverage`, `bish-security`, `bish-tests`, `bish-triage`
  - **Conversational:** `bish-chat`, `bish-grill-cards`, `bish-grill-docs`, `bish-scripts`, `bish-spec-cards`
  - **Setup-Execute:** `bish-auto-card`, `bish-onboard`, `bish-work-on-card`
  - **Bishop-level / meta:** `bish-write-skill`, `bish-audit-skills` — operate on `skills/` itself rather than a workspace's code
- `installer/` — Wix v5 project that produces the per-user MSI. See `installer/README.md`.
- `notes/_archive/` — pre-grill design notes; preserved for decision rationale but superseded by DIRECTION.md.

### Layers
Layered, with strict one-way dependencies. Modify in this order when implementing a feature:

1. **Bishop.Core** — entities (Workspace, Lane, Card, Tag, CardTag), domain primitives, enums. No external deps.
2. **Bishop.Data** — `BishopDbContext`, EF Core configurations, migrations, repository interfaces + implementations. References Core.
3. **Bishop.App** — MediatR `IRequest` types and handlers, application services (e.g. the terminal launcher), validators. References Core + Data.
4. **Bishop.ViewModels** — presentation-framework-agnostic ViewModels. Targets `net10.0`; references `Bishop.App` so VMs can take `IMediator`. **Must not** reference `Microsoft.UI.*`, `Windows.UI.*`, or `Microsoft.WindowsAppSDK` — compile-time absence is the layer enforcement. UI-thread marshalling goes through the `IUiDispatcher` abstraction defined here; visual mapping (e.g. `Visibility`) lives in XAML converters, not VMs.
5. **Bishop.UI** and **Bishop.Cli** — presentation peers. UI is the WinUI 3 desktop app (Views, DI composition root, `WinUiDispatcher : IUiDispatcher`); it references App + ViewModels. Cli is the `bishop` console executable; references App. Neither references Data directly.
6. **Bishop.Tests** — xUnit project under `tests/Bishop.Tests`. References whichever layers it tests.

Dependency direction: **Core → Data → App → { ViewModels → UI, Cli }**. UI and Cli go through MediatR handlers in App for everything.

### Kanban model
A workspace owns a fixed set of four system lanes — "Backlog", "To Do", "Doing", "Done" — seeded on workspace creation; user-defined lanes are not supported. Cards belong to a workspace (via `WorkspaceId` FK) and carry their lane membership as a `LaneName` string + ordered `Position`. Tags are workspace-scoped (with optional colour); a card carries at most one tag as a nullable `TagName` string on the `Cards` table.

### CLI surface (`bishop`)
The `bishop` console executable is the primary integration surface for skills (e.g. `bish-work-on-card`). Unversioned and additive-only — commands and flags are not renamed or removed once shipped.

- `bishop workspace list [--json]`
- `bishop workspace current [--json]` — resolves the workspace from the current working directory by ancestor match
- `bishop workspace init [--path <dir>] [--name <name>]` — register a directory (defaults to cwd) as a workspace and seed the default lanes; idempotent (no-op when fully seeded, fills gaps when partial)
- `bishop workspace set-github <owner/repo> [-w]` — link this workspace to a GitHub repo (used by `bishop card push`)
- `bishop workspace unset-github [-w]` — remove the GitHub repo link
- `bishop card add --lane <name> --title <text> [--description <text> | --description-file <path>] [--tag <name>] [--bottom] [-w <workspace>]` — inserts at the top of the lane by default; `--bottom` appends to the end
- `bishop card list [-w] [--json]`
- `bishop card view <card-id> [-w] [--json]`
- `bishop card move <card-id> --to-lane <name> --to-position <int> [-w]` — moving a card into the system `Done` lane auto-closes it (and its linked GitHub issue, if any); moving out of `Done` auto-reopens
- `bishop card edit <card-id> [--title <t>] [--description <d> | --description-file <path> | --append-description-file <path>] [--tag <name>] [--to-lane <name>] [--no-close] [-w]` — updates only the supplied fields; pass `--tag ""` to clear the tag; `--to-lane` moves the card after editing (auto-closes on move into `Done` unless `--no-close` is set)
- `bishop card claim [--lane <name>] [--tag <name>] [-w] [--json]` — picks the top card from the source lane (default "To Do"), moves it to "Doing", and emits its details; `--tag` restricts the pick to the first card carrying that tag; exits non-zero if no matching card exists
- `bishop card remove <card-id> [-w]`
- `bishop card push [<card-id>] [--lane <name>] [--dry-run] [-w]` — push a single card by ID, or all unlinked cards in a lane; `--lane` and card-id are mutually exclusive; `--dry-run` previews without calling gh
- `bishop card close <card-id> [-w]` — mark a card as closed; also closes the linked GitHub issue via `gh` if the card has been pushed
- `bishop card reopen <card-id> [-w]` — reopen a closed card; also reopens the linked GitHub issue via `gh` if the card has been pushed
- `bishop lane list [-w]` — lanes are fixed (Backlog / To Do / Doing / Done); no user-mutable lane CRUD
- `bishop tag list [-w]` — list all tags defined in the workspace
- `bishop install-skills` — copies the bundled skills under `skills/` to `%USERPROFILE%\.claude\skills\`. Run once on a fresh install; idempotent.
- `bishop batch create --name <text> [--branch <name>] [--base <branch>] [--cards <n,...>] [--tag <name>] [--lane <name>] [-w]` — create a batch, provision a git worktree, and optionally assign cards by number, tag, or lane
- `bishop batch edit <name> --new-name <text> [-w]` — rename a batch
- `bishop batch list [--json]`
- `bishop batch view <name> [--json]`
- `bishop batch add-card <name> <card-id> [-w]`
- `bishop batch remove-card <name> <card-id> [-w]`
- `bishop batch run <name> [--resume] [--model <model-id>]` — run a batch end-to-end in its worktree via `bish-auto-card`; stops on card failure; `--resume` continues from the next undone card
- `bishop batch complete <name> [-w]` — merge the batch branch into local main with `--no-ff`, close Done cards, and mark the batch closed; never pushes or calls `gh`; on conflict aborts and exits non-zero with the conflicting file list
- `bishop batch abandon <name> [-w]` — abandon a batch and remove its worktree
- `bishop batch prune [-w]` — remove worktrees for completed or abandoned batches
- `bishop skill bootstrap [--json]` — emit workspace + tag/lane info for a skill preamble; non-zero exit if not in a workspace
- `bishop context print [--section <name>] [-w]` — print the workspace CONTEXT.md file, or a single named H2 section
- `bishop context-pack <skill-name> [--card <n>] [-w] [--list]` — emit a pre-stuffed JSON context bundle (workspace + git + skill-specific data + conventions); `--list` enumerates registered providers
- `bishop hook check-path` — `PreToolUse` hook: reads tool-use JSON from stdin and exits non-zero if the target path is outside the workspace; only active when `BISHOP_AUTO_CARD` is set

Card identifiers accept a workspace-scoped Number (`42`, `#42`). CLI output renders `#N` (e.g. `#42`) rather than the full UUID.

### Current UI scope
Bishop.UI is the interactive surface; the CLI remains the automation surface for skills. UI affordances by entity:

- **Workspaces:** left-hand list with drag-to-reorder; add-workspace dialog (create new folder or attach existing); per-workspace "Claude" button (Windows Terminal + `claude` at the workspace path) and a plain "Terminal" button (Windows Terminal at the workspace path, no Claude); per-workspace "Commits" button that opens a flyout of recent git commits (click to open in GitHub when a repo is linked, otherwise copies the full SHA); per-workspace notes panel below the kanban (persisted text + drag-to-resize; expanded state stored per workspace).
- **Lanes:** fixed system set (Backlog / To Do / Doing / Done). No add / rename / delete / reorder affordances — the workflow is intentionally locked down.
- **Cards:** view detail dialog; edit title/description/tags; delete; drag-and-drop between lanes and within a lane (writes position immediately).
- **Tags:** create / remove / recolour via the card edit dialog.
- **Skills:** launcher buttons on each card and on the workspace header — see [Skill integration](#skill-integration).
- **Theming:** dark theme applied across shell, nav, board chrome, and dialogs.

### Skill integration
Bundled Claude Code skills under `skills/` ship with `bishop.exe` and are installed to `%USERPROFILE%\.claude\skills\` via `bishop install-skills` (overwrites on each run). They group into four categories — Review (`bish-arch`, `bish-dead-code`, `bish-audit-docs`, `bish-coverage`, `bish-security`, `bish-tests`, `bish-triage`), Conversational (`bish-chat`, `bish-grill-cards`, `bish-grill-docs`, `bish-scripts`), Setup-Execute (`bish-auto-card`, `bish-onboard`, `bish-work-on-card`), and Bishop-level / meta (`bish-write-skill`, `bish-audit-skills`, which operate on `skills/` itself rather than a workspace's code). See [docs/SKILL_FAMILY.md](docs/SKILL_FAMILY.md) for the category rationale. Each skill is a directory containing a `SKILL.md` whose YAML frontmatter declares:

- `name` — skill identifier (required).
- `description` — user-facing summary.
- `allowed-tools` — comma-separated Claude Code tool allowlist.
- `bishop.scope` — `card` (button on each card) or `workspace` (button on workspace header); null/missing → not surfaced in the UI.
- `bishop.command` — slash-command template launched on click. Placeholders: `{{card_number}}` (card scope) and `{{workspace_path}}` (workspace scope).
- `bishop.stage` — optional boolean (`true`/`false`, default `false`). When `true`, clicking the skill button opens a staging dialog before launch; the user can type optional extra text that is appended to the rendered command.
- `bishop.stage_prompt` — optional string. Overrides the placeholder text shown inside the staging dialog (e.g. "Enter a card number to work on").

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
- **Full GitHub Issues / Projects sync.** Basic integration is shipped (`bishop workspace set-github`, `bishop card push`); bidirectional sync and a broader issue-backlog ingestion flow remain the boundary.
- **Plugin system.** Future tabs ship as in-tree code, not external plugins.
