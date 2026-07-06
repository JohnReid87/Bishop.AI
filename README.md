# Bishop.AI

A Windows desktop app for managing AI-assisted coding work across many local repositories. Single-user, local-first, Windows-only.

See **[OVERVIEW.md](OVERVIEW.md)** for what Bishop is, who it's for, and how you'd use it.

## Getting started

- Requires: .NET 10.0 SDK, Windows 10 version 1809 (build 17763) or later, Visual Studio or VS Code with .NET extension
- Build: `dotnet build Bishop.AI.slnx`
- Tests: `dotnet test`
- No installer — Bishop is built from source and run directly (there is no packaged distribution).
- Skills: run `bishop install-skills` once to populate `~/.claude/skills/` with the bundled Claude Code skills. They group into six categories — see [docs/SKILL_FAMILY.md](docs/SKILL_FAMILY.md) for the rationale:
  - **Conversational:** `bish-grill-cards`, `bish-grill-docs`, `bish-scripts`, `bish-spec-cards`
  - **Code:** `bish-arch`, `bish-dead-code`, `bish-security`
  - **Tests:** `bish-coverage`, `bish-tests`
  - **Review:** `bish-audit-docs`, `bish-review-batch`, `bish-triage`
  - **Setup-Execute:** `bish-auto-card`, `bish-life-add`, `bish-life-init`, `bish-life-standup`, `bish-onboard`, `bish-work-on-card` (the `bish-life-*` skills operate on the bishop.life data file rather than a workspace)
  - **Bishop-level / meta:** `bish-write-skill`, `bish-audit-skills` — operate on `skills/` itself rather than a workspace's code

See [CONTEXT.md](CONTEXT.md) for tech stack, architecture, and conventions, and [DIRECTION.md](DIRECTION.md) for scope decisions.
