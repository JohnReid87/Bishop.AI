# Bishop.Tests

## Structure

Mirrors `bishop/src/` layout — one top-level folder per source project, with
feature subfolders inside:

```
bishop/tests/Bishop.Tests/
├── App/            ← handler + service tests (Batches/, Cards/, Context/, Findings/,
│                     Git/, Lanes/, Scripts/, Services/, Skills/, Tags/, Workspaces/)
├── Architecture/   ← layer-rule tests (e.g. AppServicesLocatorRuleTests)
├── Cli/            ← CLI command tests, mirroring Bishop.Cli's command folders
├── Core/           ← entity + domain-primitive tests (BatchTests, TagNamesTests, …)
├── Data/           ← DbContext, query-extension, and migration tests
├── Docs/           ← doc drift tests (ContextMdFactBlockTests, schema tests)
├── ViewModels/     ← ViewModel tests incl. layer rules (CodeBehindLayerRuleTests,
│                     DataAccessLayerRuleTests)
├── DbFixture.cs    ← shared in-memory SQLite fixture
└── Bishop.Tests.csproj
```

## Conventions

**AAA style is mandatory.** Every test must have `// Arrange`, `// Act`, and `// Assert` section markers with blank lines between them.

**Real SQLite for EF Core handler tests.** Handler tests (`CardHandlerTests`, `LaneHandlerTests`, `WorkspaceHandlerTests`) and service tests that touch the DB (`AppSettingsServiceTests`) use `IClassFixture<DbFixture>` to get an in-memory SQLite database. Do not mock `BishopDbContext` or `DbSet<T>` — EF Core mocking is brittle and a documented antipattern. Use a real (in-memory) connection instead.

**NSubstitute for services with process or network boundaries.** Services that shell out (e.g. `GitCli`, `TerminalLauncher`) accept delegates or interfaces at their boundaries so tests can inject fakes without spawning real processes. Use NSubstitute for interface fakes where needed.

**80% line coverage per project is the target.** Run `.\coverage.ps1` at the repo root to generate a coverage report.

## Not present: Bishop.UI.Tests

A `bishop/tests/Bishop.UI.Tests/` project does not exist and is not planned. WinUI 3 rendering and code-behind orchestration are intentionally out of test scope (see CONTEXT.md — "No tests target `net10.0-windows`"). `bishop/tests/Bishop.Tests/Bishop.Tests.csproj` is the only test project in the solution.
