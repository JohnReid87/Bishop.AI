# Bishop.IO — Project Context

## Domain
A Windows desktop app for managing AI-assisted coding workflows. The user has many local code directories (workspaces); Bishop.IO is a single place to organise them, track work against each one via a per-workspace kanban board, and launch the Claude Code Windows app pointed at the chosen directory. Single-user, local-first. The name is a nod to Bishop, the synthetic science officer in *Aliens*.

## Tech Stack
- **UI:** WinUI 3 (Windows App SDK), unpackaged.
- **Architecture pattern:** MVVM via `CommunityToolkit.Mvvm` (source generators for `ObservableProperty` / `RelayCommand`).
- **App layer:** MediatR for commands/queries.
- **DI:** `Microsoft.Extensions.DependencyInjection` via the generic host.
- **Data:** EF Core 8 + SQLite. DB file at `%AppData%\Bishop.IO\bishop.db`.
- **Markdown:** Markdig + a WinUI markdown renderer for card descriptions.
- **Testing:** xUnit + FluentAssertions. Handlers and repos tested against in-memory or temp SQLite. No UI tests for MVP.
- **Target framework:** `net8.0-windows10.0.19041.0` (or current LTS WindowsAppSDK target).

## Architecture
Layered, with strict one-way dependencies. Modify in this order when implementing a feature:

1. **Bishop.Core** — entities (Workspace, Lane, Card), domain primitives, enums. No external deps.
2. **Bishop.Data** — `BishopDbContext`, EF Core configurations, migrations, repository interfaces + implementations. References Core.
3. **Bishop.App** — MediatR `IRequest` types and handlers, application services (e.g. the Claude Code launcher), validators. References Core + Data.
4. **Bishop.UI** — WinUI 3 App, Views (XAML), ViewModels, value converters, DI composition root. References App.
5. **Bishop.Tests** — xUnit project. References whichever layers it tests.

Dependency direction: **Core → Data → App → UI**. UI never references Data directly; it goes through MediatR handlers in App.

## Conventions
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
