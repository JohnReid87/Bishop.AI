# Bishop.AI

A Windows desktop app for managing AI-assisted coding workflows. A left-hand workspace nav (each workspace bound to a local directory) and a right-hand pane housing a per-workspace kanban board plus a launch-terminal button that opens Windows Terminal with `claude` at the workspace path. Single-user, local-first, Windows-only.

State is mutated via the `bishop` CLI (used by skills like `work-on-card-bishop`); the WinUI app is currently a read-only viewer.

## Getting started

Build the solution with `dotnet build Bishop.AI.slnx`. Run `dotnet publish src/Bishop.Cli` to produce `bishop.exe`, and `dotnet publish src/Bishop.UI` to produce the WinUI app.

See [CONTEXT.md](CONTEXT.md) for tech stack, architecture, and conventions.
