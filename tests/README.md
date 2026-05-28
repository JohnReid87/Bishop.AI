# Bishop.Tests

## Structure

Mirrors `src/` layout:

```
tests/Bishop.Tests/
├── App/
│   ├── Cards/          ← CardHandlerTests
│   ├── Git/            ← GetRecentCommitsTests
│   ├── Lanes/          ← LaneHandlerTests
│   ├── Ping/           ← PingQueryHandlerTests
│   ├── Settings/       ← AppSettingsServiceTests
│   ├── Skills/         ← DiscoverSkillsQueryHandlerTests
│   ├── Terminal/       ← TerminalLauncherTests, TerminalSnapTests
│   ├── Workspaces/     ← WorkspaceHandlerTests
│   ├── BishopDbConnectionStringTests.cs
│   └── DatabaseInitializerTests.cs
├── Data/               ← DesignTimeDbContextFactoryTests
├── DbFixture.cs        ← shared in-memory SQLite fixture
└── Bishop.Tests.csproj
```

## Conventions

**AAA style is mandatory.** Every test must have `// Arrange`, `// Act`, and `// Assert` section markers with blank lines between them.

**Real SQLite for EF Core handler tests.** Handler tests (`CardHandlerTests`, `LaneHandlerTests`, `WorkspaceHandlerTests`) and service tests that touch the DB (`AppSettingsServiceTests`) use `IClassFixture<DbFixture>` to get an in-memory SQLite database. Do not mock `BishopDbContext` or `DbSet<T>` — EF Core mocking is brittle and a documented antipattern. Use a real (in-memory) connection instead.

**NSubstitute for services with process or network boundaries.** Services that shell out (e.g. `GitCli`, `TerminalLauncher`) accept delegates or interfaces at their boundaries so tests can inject fakes without spawning real processes. Use NSubstitute for interface fakes where needed.

**80% line coverage per project is the target.** Run `.\coverage.ps1` at the repo root to generate a coverage report.
