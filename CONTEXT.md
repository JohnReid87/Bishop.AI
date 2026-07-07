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
- **Testing:** xUnit + FluentAssertions. Handlers in `Bishop.App` are tested against in-memory or temp SQLite; ViewModels in `Bishop.ViewModels` are testable against the `IUiDispatcher` abstraction. No tests target `net10.0-windows`; visual rendering and `xaml.cs` orchestration are out of scope.
- **Target framework:** `net10.0` for Core / Data / App / ViewModels / Cli / Tests; `net10.0-windows10.0.19041.0` for Bishop.UI.

## Architecture

### Repository layout
Top-level filesystem is split into per-app peers — `bishop/` (the Bishop.AI desktop app and its CLI) and `life/` (the Bishop.Life sibling app). Each peer carries its own `src/` and `tests/`. This mirrors the `.slnx` grouping verbatim.

- `bishop/src/` — Bishop.AI .NET projects (Core, Data, App, ViewModels, Cli, UI).
- `bishop/tests/Bishop.Tests/` — xUnit project for Bishop.AI.
- `life/src/` — Bishop.Life .NET projects (Core, App).
- `life/tests/Bishop.Life.Tests/` — xUnit project for Bishop.Life.
- `life/tools/Bishop.Life.SchemaCodegen/` — Roslyn-syntax-driven console tool that emits `life/src/Bishop.Life.App/Assets/js/schema.d.ts` from `Bishop.Life.Core/Schema/**`. Runs from a `BeforeBuild` target in `Bishop.Life.App.csproj` with `Inputs`/`Outputs` so incremental builds skip when neither the schema nor the tool sources changed.
- `skills/` — vendored Claude Code skill files shipped with `bishop.exe` and installed to `~/.claude/skills/` via `bishop install-skills`. Grouped into six categories (see [docs/SKILL_FAMILY.md](docs/SKILL_FAMILY.md) for rationale):
  - **Conversational:** `bish-grill-cards`, `bish-scripts`, `bish-spec-cards`
  - **Code:** `bish-arch`, `bish-dead-code`, `bish-security`
  - **Tests:** `bish-coverage`, `bish-tests`
  - **Review:** `bish-audit-docs`, `bish-review-batch`
  - **Setup-Execute:** `bish-auto-card`, `bish-life-add`, `bish-life-init`, `bish-life-standup`, `bish-onboard`, `bish-work-on-card` (the `bish-life-*` skills operate on the bishop.life data file rather than a workspace)
  - **Bishop-level / meta:** _(none currently — skills that operate on `skills/` itself rather than a workspace's code live here)_
- `notes/_archive/` — pre-grill design notes; preserved for decision rationale but superseded by DIRECTION.md.

### Layers
Layered, with strict one-way dependencies. Modify in this order when implementing a feature:

1. **Bishop.Core** — entities (Workspace, Card, AppSetting, WorkspaceSkillRun, Finding, Batch), domain primitives, enums. No external deps.
2. **Bishop.Data** — `BishopDbContext`, EF Core configurations, migrations, query-extension helpers (`BatchQueries`, etc.). No repository abstractions. References Core.
3. **Bishop.App** — MediatR `IRequest` types and handlers, application services (e.g. the terminal launcher), validators. References Core + Data.
4. **Bishop.ViewModels** — presentation-framework-agnostic ViewModels. Targets `net10.0`; references `Bishop.App` so VMs can take `IMediator`. **Must not** reference `Microsoft.UI.*`, `Windows.UI.*`, or `Microsoft.WindowsAppSDK` — compile-time absence is the layer enforcement. UI-thread marshalling goes through the `IUiDispatcher` abstraction defined here; visual mapping (e.g. `Visibility`) lives in XAML converters, not VMs.
5. **Bishop.UI** and **Bishop.Cli** — presentation peers. UI is the WinUI 3 desktop app (Views, DI composition root, `WinUiDispatcher : IUiDispatcher`); it references App + ViewModels. Cli is the `bishop` console executable; references App. Neither references Data directly.
6. **Bishop.Tests** — xUnit project under `bishop/tests/Bishop.Tests`. References whichever layers it tests.

Dependency direction: **Core → Data → App → { ViewModels → UI, Cli }**. UI and Cli go through MediatR handlers in App for everything.

### Kanban model
A workspace owns a fixed set of four system lanes, seeded on workspace creation; user-defined lanes are not supported. Cards belong to a workspace (via `WorkspaceId` FK) and carry their lane membership as a `LaneName` string + ordered `Position`.

The lane names are:

<!-- bishop-fact:lanes -->
- `Backlog`
- `To Do`
- `Doing`
- `Done`
<!-- /bishop-fact -->

Tags are a global fixed set of 8 names (each with a default colour from `BrandTagPalette`); tags are not user-mutable from the UI. A card carries at most one tag as a nullable `TagName` string on the `Cards` table.

The tag names are:

<!-- bishop-fact:tags -->
- `feature`
- `bug`
- `chore`
- `docs`
- `arch`
- `test`
- `spike`
- `security`
<!-- /bishop-fact -->

The `<!-- bishop-fact:NAME --> … <!-- /bishop-fact -->` markers wrap load-bearing factual lists; their contents are asserted against the corresponding code constants by `Bishop.Tests.Docs.ContextMdFactBlockTests`, so editing the block to disagree with code fails a test. New fact-blocks may be added the same way as more facts become drift-prone.

### CLI surface (`bishop`)
The `bishop` console executable is the primary integration surface for skills (e.g. `bish-work-on-card`). Unversioned and additive-only — commands and flags are not renamed or removed once shipped.

- `bishop workspace list [--json]`
- `bishop workspace current [--json]` — resolves the workspace from the current working directory by ancestor match
- `bishop workspace init [--path <dir>] [--name <name>]` — register a directory (defaults to cwd) as a workspace and seed the default lanes; idempotent (no-op when fully seeded, fills gaps when partial)
- `bishop workspace remove [--yes] [--dry-run] [-w]` — archive a workspace (soft-delete); card data is preserved; deletes the workspace's `.bishop/` directory if present
- `bishop workspace purge (--path <dir> | --name <name>) [--yes] [--dry-run]` — hard-delete an *archived* workspace and all its cards
- `bishop workspace record-skill-run --skill <name> --sha <sha> [-w]` — record that a review skill ran against the workspace at a given commit
- `bishop card create --lane <name> --title <text> [--description <text> | --description-file <path>] [--tag <name>] [--bottom] [-w <workspace>]` — inserts at the top of the lane by default; `--bottom` appends to the end
- `bishop card list [-w] [--json]`
- `bishop card show <card-id> [-w] [--json]`
- `bishop card move <card-id> --to-lane <name> --to-position <int> [-w]` — moving a card into the system `Done` lane auto-closes it; moving out of `Done` auto-reopens
- `bishop card edit <card-id> [--title <t>] [--description <d> | --description-file <path> | --append-description-file <path>] [--tag <name>] [--to-lane <name>] [--commit-hash <sha>] [--commit-branch <name>] [-w]` — updates only the supplied fields; pass `--tag ""` to clear the tag; `--to-lane` moves the card after editing (auto-closes on move into `Done`)
- `bishop card claim [--lane <name>] [--tag <name>] [-w] [--json]` — picks the top card from the source lane (default "To Do"), moves it to "Doing", and emits its details; `--tag` restricts the pick to the first card carrying that tag; exits non-zero if no matching card exists
- `bishop card remove <card-id> [-w]`
- `bishop card close <card-id> [-w]` — mark a card as closed
- `bishop card reopen <card-id> [-w]` — reopen a closed card
- `bishop card set-commit <card-id> --hash <sha> --branch <name> [-w]` — record the commit hash and branch that implemented a card
- `bishop lane list [-w]` — lanes are fixed (Backlog / To Do / Doing / Done); no user-mutable lane CRUD
- `bishop tag list [-w]` — list all tags defined in the workspace
- `bishop install-skills` — copies the bundled skills under `skills/` to `%USERPROFILE%\.claude\skills\`. Run once on a fresh install; idempotent.
- `bishop batch create --name <text> [--branch <name>] [--base <branch>] [--cards <n,...>] [--tag <name>] [--lane <name>] [-w]` — create a batch, provision a git worktree, and optionally assign cards by number, tag, or lane
- `bishop batch edit <name> --new-name <text> [-w]` — rename a batch
- `bishop batch list [--all] [--json]` — lists non-Closed batches; `--all` includes Closed
- `bishop batch show <name> [--json]`
- `bishop batch add-card <name> <card-id> [-w]`
- `bishop batch remove-card <name> <card-id> [-w]`
- `bishop batch run <name> [--resume] [--model <model-id>]` — run a batch end-to-end in its worktree via `bish-auto-card`; stops on card failure; `--resume` continues from the next undone card
- `bishop batch rescue <name> [--yes]` — recover an interrupted run: clear the lock when its PID is dead, reset a dirty worktree (confirmation required unless `--yes`), and re-queue the card stranded in `Doing`; refuses (non-zero) if the lock PID is still alive. Unblocks `run --resume` after a killed run without manual `git` surgery
- `bishop batch merge <name> [-w]` — merge the batch branch into the base branch (normally local `main`) with `--no-ff`; never pushes or calls `gh`; on conflict aborts and exits non-zero with the conflicting file list
- `bishop batch clean-up <name> [-w]` — remove the worktree, delete the branch, close the batch, and close any Done-lane cards assigned to it (requires `merge` first); prints `Closed card #N` per card
- `bishop batch abandon <name> [-w]` — abandon a batch and remove its worktree
- `bishop batch remove <name>` — delete a closed batch record from the database; cards stay on the board
- `bishop batch prune [-w]` — remove worktrees for completed or abandoned batches
- `bishop findings record --skill <name> --sha <sha> --file <path> [--project <name>] [-w]` — persist a review skill's findings JSON (`-` for stdin) to the DB; surfaced in the Findings page; `--project` scopes the run for project-scoped skills
- `bishop skill bootstrap [--json]` — emit workspace + tag/lane info for a skill preamble; non-zero exit if not in a workspace
- `bishop context print [--section <name>] [-w]` — print the workspace CONTEXT.md file, or a single named H2 section
- `bishop context-pack <skill-name> [--card <n>] [-w] [--list]` — emit a pre-stuffed JSON context bundle (workspace + git + skill-specific data + conventions); `--list` enumerates registered providers
- `bishop context-pack life-standup` — emit the bishop.life stand-up context pack as JSON (plan summary plus upcoming Google Calendar events when authorized); reads `bishop.life.json`, not the workspace DB
- `bishop life auth google` — installed-app OAuth flow granting read access to the primary Google Calendar; requires the Google OAuth client env vars; stores the refresh token DPAPI-encrypted
- `bishop life speak [<text>]` — synthesize text (stdin when omitted) to speech and play synchronously
- `bishop life speak-prelude` — speak a short random acknowledgement to fill pre-context silence at stand-up launch
- `bishop hook check-path` — `PreToolUse` hook: reads tool-use JSON from stdin and exits non-zero if the target path is outside the workspace; only active when `BISHOP_AUTO_CARD` is set
- `bishop hook speak-on-stop` — `Stop` hook: speaks the last assistant message aloud when the active skill is an opted-in `bish-life-*` skill; never blocks the conversation

Card identifiers accept a workspace-scoped Number (`42`, `#42`). CLI output renders `#N` (e.g. `#42`) rather than the full UUID.

### Current UI scope
Bishop.UI is the interactive surface; the CLI remains the automation surface for skills. UI affordances by entity:

- **Workspaces:** left-hand list with drag-to-reorder; add-workspace dialog (create new folder or attach existing); per-workspace "Claude" button (Windows Terminal + `claude` at the workspace path) and a plain "Terminal" button (Windows Terminal at the workspace path, no Claude); per-workspace "Commits" button that opens a flyout of recent git commits (click to open in GitHub when a repo is linked, otherwise copies the full SHA); per-workspace notes panel below the kanban (persisted text + drag-to-resize; expanded state stored per workspace).
- **Lanes:** fixed system set (Backlog / To Do / Doing / Done). No add / rename / delete / reorder affordances — the workflow is intentionally locked down.
- **Cards:** view detail dialog; edit title/description/tags; delete; drag-and-drop between lanes and within a lane (writes position immediately).
- **Tags:** fixed global set (see [Kanban model](#kanban-model)). Assigned per card via the card edit dialog; no add / rename / recolour affordances.
- **Skills:** launcher buttons on each card and on the workspace header, grouped by `bishop.category`. Review/analysis skills (the `Code` / `Tests` / `Review` categories) are *not* shown in the launcher — their single home is the Monitoring view, where their run history and "Run now" live. See [Skill integration](#skill-integration).
- **Workspace sections:** the workspace detail page toggles between **Board**, **Monitoring**, and **Batches** views. Monitoring lists every installed review-category skill (`Code` / `Tests` / `Review`) with its recorded runs (`Last run` / `Commits since`, fed by `bishop findings record`), links to the findings reports, and a per-skill "Run now"; Batches manages batch lifecycle, and open batches also render on the board as accent-bordered card groups (see BRAND.md → Batch accent palette).
- **Scripts:** a nav-pane button opens the PowerShell script launcher over `%AppData%\Bishop.AI\scripts\` (populated by hand or via `bish-scripts`).
- **Settings:** app-level settings dialog (General / Workspaces / Skills sections; version, database path, build info).
- **Theming:** dark theme applied across shell, nav, board chrome, and dialogs.

### Bishop.Life host↔viewer wire contract
`Bishop.Life.App` hosts a `WebView2` whose page is `Assets/index.html` + TypeScript modules under `Assets/js/` (compiled to `Assets/js-build/` at build time). The host and viewer talk over `CoreWebView2.PostWebMessageAsJson` / `WebMessageReceived`, exchanging JSON envelopes with a `type` discriminator (host→viewer envelopes for `speak.*`, `terminal:*`, `transcript:event`, plus a no-discriminator plan-state envelope; viewer→host envelopes for `standup`/`init`/`add` bare strings, `mutate`, `terminal:input`, `terminal:resize`).

Every wire shape is a `public sealed record` under `Bishop.Life.Core/Schema/Envelopes/`, alongside the plan schema records in `Bishop.Life.Core/Schema/`. The `Bishop.Life.SchemaCodegen` tool walks both directories via Roslyn and emits `life/src/Bishop.Life.App/Assets/js/schema.d.ts` — the single TypeScript source of truth for both ends of the channel. The file is regenerated on every `Bishop.Life.App` build (Inputs/Outputs gate skip incremental builds) and committed for review visibility.

Adding a new envelope: drop a `public sealed record` under `Schema/Envelopes/`, use it from the relevant controller (`SpeakController` / `StandupController` / `PlanController`), rebuild — `schema.d.ts` updates automatically.

### Skill integration
Bundled Claude Code skills under `skills/` ship with `bishop.exe` and are installed to `%USERPROFILE%\.claude\skills\` via `bishop install-skills` (overwrites on each run). They group into six categories — Conversational (`bish-grill-cards`, `bish-scripts`, `bish-spec-cards`), Code (`bish-arch`, `bish-dead-code`, `bish-security`), Tests (`bish-coverage`, `bish-tests`), Review (`bish-audit-docs`, `bish-review-batch`), Setup-Execute (`bish-auto-card`, `bish-life-add`, `bish-life-init`, `bish-life-standup`, `bish-onboard`, `bish-work-on-card` — the `bish-life-*` skills operate on the bishop.life data file rather than a workspace), and Bishop-level / meta (currently empty — reserved for skills that operate on `skills/` itself rather than a workspace's code). See [docs/SKILL_FAMILY.md](docs/SKILL_FAMILY.md) for the category rationale. Each skill is a directory containing a `SKILL.md` whose YAML frontmatter declares:

- `name` — skill identifier (required).
- `description` — user-facing summary.
- `allowed-tools` — comma-separated Claude Code tool allowlist.
- `bishop.scope` — `card` (button on each card) or `workspace` (button on workspace header); null/missing → not surfaced in the UI.
- `bishop.command` — slash-command template launched on click. Placeholders: `{{card_number}}` (card scope) and `{{workspace_path}}` (workspace scope).
- `bishop.stage` — optional boolean (`true`/`false`, default `false`). When `true`, clicking the skill button opens a staging dialog before launch; the user can type optional extra text that is appended to the rendered command.
- `bishop.stage_prompt` — optional string. Overrides the placeholder text shown inside the staging dialog (e.g. "Enter a card number to work on").

`DiscoverSkillsQueryHandler` (Bishop.App) scans `~/.claude/skills/` at workspace load and feeds two launcher button groups (card + workspace) in `WorkspaceDetailPage`, built by `SkillMenuBuilder` grouped under `bishop.category` headers. `BoardSkillsCoordinator` excludes review-category skills (`SkillCategoryExtensions.IsMonitored`) from those groups so they surface only in Monitoring; `WorkspaceMonitoringViewModel` uses the same predicate to pick which skills it tracks, keeping a single UI home per skill. Clicking a launcher skill renders the template, opens Windows Terminal at the workspace path, and runs `claude "<rendered command>"` (`LaunchSkillCommandHandler`; falls back to PowerShell if `wt.exe` is unavailable). Adding a new skill: drop a directory under `skills/`, set scope + command + category, rebuild, run `bishop install-skills`.

## Conventions
- **CLI is the automation surface; UI is the interactive surface.** Skills mutate state through `bishop` CLI invocations; humans use the UI directly for card / lane / tag edits. Both writers share the SQLite DB in WAL mode.
- **Naming:** standard .NET (PascalCase types/members, _camelCase private fields, `I`-prefix interfaces). One public type per file.
- **Async:** all I/O async; `Async` suffix; pass `CancellationToken` through handlers.
- **MVVM:** ViewModels derive from `ObservableObject`; commands use `[RelayCommand]`. Code-behind (`*.xaml.cs`) is limited to view mechanics (drag/drop, focus, lifecycle, visual-tree construction) and **must not reference `Bishop.App`** (no `IMediator`, handlers, App services, or App result-DTOs); all application calls and state go through the ViewModel. Sole exception: the DI composition root `App.xaml.cs`. An architecture test (`CodeBehindLayerRuleTests`) enforces this by scanning every `*.xaml.cs` under `bishop/src/Bishop.UI` for `Bishop.App` references, with a tracked allowlist for the remaining offenders.
- **MediatR:** one request + one handler per file, colocated. Validators (if any) live next to the request.
- **EF Core:** entities are POCOs free of EF/persistence concerns; configuration via `IEntityTypeConfiguration<T>` classes, not data annotations. Entities may carry guarded domain-transition methods (e.g. `Batch.Close`, `Batch.TransitionToWorking`) that enforce invariants without knowing about the DB.
- **Data access (Contract):** handlers in `Bishop.App` inject `IDbContextFactory<BishopDbContext>` directly — no Repository abstraction. Cross-aggregate invariants (e.g. assigning a card to a batch) live in `internal static` helper classes (e.g. `BatchAssignment`) called by the handler inside a caller-opened `IsolationLevel.Serializable` transaction. An architecture test (`DataAccessLayerRuleTests`) enforces this: no type in `Bishop.Data` ending in `Repository`, and no handler injecting one.
- **Migrations:** generated via `dotnet ef migrations add` from the Data project; checked in. When iterating on schema, point EF at a throwaway scratch DB (set `BISHOP_DB` to a temp path) and commit a migration only once the feature's shape has settled — avoids churn from add/revert cycles polluting the migration chain. The generated `*.Designer.cs` files and `BishopDbContextModelSnapshot.cs` are marked `linguist-generated` in `.gitattributes` so GitHub collapses them in diffs and drops them from language stats.
- **Tests:** Arrange/Act/Assert with blank lines between sections; `FluentAssertions` for all assertions.
- **Formatting:** `dotnet format` clean; `.editorconfig` at repo root.

## Out of scope
Recorded so future-me doesn't drift into them:
- **Cross-platform** (Mac / Linux). Windows-only by design.
- **Multi-user / cloud sync.** Single-user local app. No accounts, no servers.
- **GitHub Issues / Projects sync.** Removed in cards #973 and #974 for security (prompt-injection surface via imported issue bodies) and disuse. The CLI surface, MediatR handlers, UI dialogs, `gh` integration, and the `Workspace.GitHubRepo` / `Card.GitHubIssueNumber` / `Card.GitHubPushedAt` columns are all gone.
- **Plugin system.** Future tabs ship as in-tree code, not external plugins.
