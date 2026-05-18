# Bishop.AI

A Windows desktop app for managing AI-assisted coding workflows. A left-hand workspace nav (each workspace bound to a local directory) and a right-hand pane housing a per-workspace kanban board plus a launch-terminal button that opens Windows Terminal with `claude` at the workspace path. Single-user, local-first, Windows-only.

State is mutated via the `bishop` CLI (used by skills like `work-on-card-bishop`); the WinUI app is currently a read-only viewer.

## Getting started

- Requires: .NET 10.0 SDK, Windows 10+, Visual Studio or VS Code with .NET extension
- Build: `dotnet build Bishop.AI.slnx`
- Tests: `dotnet test`
- Per-user MSI: `pwsh installer/build.ps1` (one-time prereq: `dotnet tool install --global wix --version 5.0.2`). Output at `installer/bin/Release/Bishop.AI.msi`. After installing the MSI, run `bishop install-skills` once to populate `~/.claude/skills/` with the bundled Claude Code skills.

See [CONTEXT.md](CONTEXT.md) for tech stack, architecture, and conventions, and [DIRECTION.md](DIRECTION.md) for scope decisions.
