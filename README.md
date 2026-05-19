# Bishop.AI

A Windows desktop app for managing AI-assisted coding workflows. A left-hand workspace nav (each workspace bound to a local directory) and a right-hand pane housing a per-workspace kanban board, a "Claude" button that opens Windows Terminal with `claude` at the workspace path, and a plain "Terminal" button that opens Windows Terminal at the workspace path without launching Claude. Single-user, local-first, Windows-only.

The UI is the primary surface for kanban work (cards, lanes, tags); the `bishop` CLI is the automation surface for skills like `bish-work-on-card`.

## Getting started

- Requires: .NET 10.0 SDK, Windows 10 version 1809 (build 17763) or later, Visual Studio or VS Code with .NET extension
- Build: `dotnet build Bishop.AI.slnx`
- Tests: `dotnet test`
- Per-user MSI: `pwsh installer/build.ps1` (one-time prereq: `dotnet tool install --global wix --version 5.0.2`). Output at `installer/bin/Release/Bishop.AI.msi`.
- After MSI install: run `bishop install-skills` once to populate `~/.claude/skills/` with the bundled Claude Code skills (`bish-work-on-card`, `bish-grill-me`, `bish-audit-docs`).

See [CONTEXT.md](CONTEXT.md) for tech stack, architecture, and conventions, and [DIRECTION.md](DIRECTION.md) for scope decisions.
