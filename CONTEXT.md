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
- `bishop card add --lane <name> --title <text> [--description <text> | --description-file <path>] [--tag <name>...] [-w <workspace>]`
- `bishop card list [-w] [--json]`
- `bishop card view <card-id> [-w] [--json]`
- `bishop card move <card-id> --to-lane <name> --to-position <int> [-w]`
- `bishop card remove <card-id> [-w]`
- `bishop tag list|add|remove [-w]`

Card identifiers accept the first 8 hex chars of the GUID as a short-ID prefix (ambiguous prefixes are rejected with a list of candidates on stderr).

### Current UI scope
Bishop.UI ships as a read-only viewer over the kanban:

- Left-hand workspace list with drag-to-reorder.
- Workspace board pane showing lanes and the cards in each lane.
- Per-workspace launch-terminal button (spawns Windows Terminal with `claude` at the workspace path).
- Add-workspace dialog (create new folder or attach an existing one).

The UI does not currently edit cards or tags — those mutations go through the CLI.

## Conventions
- **CLI is the primary mutation surface; UI is read-only.** Skills and humans alike change state through `bishop` invocations. The UI reflects state but does not edit it (today).
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
