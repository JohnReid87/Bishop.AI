# Bishop.AI

A Windows desktop app for managing AI-assisted coding work across many local repositories. Single-user, local-first, Windows-only.

See **[OVERVIEW.md](OVERVIEW.md)** for what Bishop is, who it's for, and how you'd use it.

## Getting started

- Requires: .NET 10.0 SDK, Windows 10 version 1809 (build 17763) or later, Visual Studio or VS Code with .NET extension
- Build: `dotnet build Bishop.AI.slnx`
- Tests: `dotnet test`
- Per-user MSI: `pwsh installer/build.ps1` (one-time prereq: `dotnet tool install --global wix --version 5.0.2`). Output at `installer/bin/Release/Bishop.AI.msi`.
- After MSI install: run `bishop install-skills` once to populate `~/.claude/skills/` with the bundled Claude Code skills. They group into four categories — see [docs/SKILL_FAMILY.md](docs/SKILL_FAMILY.md) for the rationale:
  - **Review:** `bish-arch`, `bish-dead-code`, `bish-audit-docs`, `bish-coverage`, `bish-security`, `bish-tests`, `bish-triage`
  - **Conversational:** `bish-chat`, `bish-grill-cards`, `bish-grill-docs`, `bish-scripts`, `bish-spec-cards`
  - **Setup-Execute:** `bish-auto-card`, `bish-onboard`, `bish-work-on-card`
  - **Bishop-level / meta:** `bish-write-skill`, `bish-audit-skills` — operate on `skills/` itself rather than a workspace's code

See [CONTEXT.md](CONTEXT.md) for tech stack, architecture, and conventions, and [DIRECTION.md](DIRECTION.md) for scope decisions.
