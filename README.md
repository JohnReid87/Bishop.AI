# Bishop.AI

A Windows desktop app for managing AI-assisted coding work across many local repositories. Single-user, local-first, Windows-only.

See **[OVERVIEW.md](OVERVIEW.md)** for what Bishop is, who it's for, and how you'd use it.

## Getting started

- Requires: .NET 10.0 SDK, Windows 10 version 1809 (build 17763) or later, Visual Studio or VS Code with .NET extension
- Build: `dotnet build Bishop.AI.slnx`
- Tests: `dotnet test`
- No installer — Bishop is built from source and run directly (there is no packaged distribution).
- Skills: run `bishop install-skills` once to populate `~/.claude/skills/` with the bundled Claude Code skills. They group into six categories — see [docs/SKILL_FAMILY.md](docs/SKILL_FAMILY.md) for the rationale:
  - **Conversational:** `bish-grill-cards`, `bish-scripts`, `bish-spec-cards`
  - **Code:** `bish-arch`, `bish-dead-code`, `bish-security`
  - **Tests:** `bish-coverage`, `bish-tests`
  - **Review:** `bish-audit-docs`, `bish-review-batch`
  - **Setup-Execute:** `bish-auto-card`, `bish-onboard`, `bish-work-on-card`
  - **Bishop-level / meta:** _(none currently — skills that operate on `skills/` itself rather than a workspace's code live here)_

See [CONTEXT.md](CONTEXT.md) for tech stack, architecture, and conventions, and [DIRECTION.md](DIRECTION.md) for scope decisions.
