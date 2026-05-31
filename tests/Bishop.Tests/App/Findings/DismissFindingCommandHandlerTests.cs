using Bishop.App.Findings.DismissFinding;
using Bishop.App.Findings.RecordFindings;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Findings;

public sealed class DismissFindingCommandHandlerTests : IClassFixture<DbFixture>, IDisposable
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly string _tempRoot;

    public DismissFindingCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _tempRoot = Path.Combine(Path.GetTempPath(), "bishop-dismissfinding-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = "ws-" + Guid.NewGuid().ToString("N")[..8];
        return await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, Path.Combine(_tempRoot, name)), default);
    }

    private async Task<Finding> SeedFindingAsync(Workspace ws)
    {
        const string json = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "parked",
                              "file": "src/F.cs", "rule": "R", "symbol": "S" } ] }
            """;
        await new RecordFindingsCommandHandler(_factory, TimeProvider.System)
            .Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha1"), default);

        return await _db.Findings.AsNoTracking()
            .Include(f => f.Run)
            .SingleAsync(f => f.Run.WorkspaceId == ws.Id);
    }

    [Fact]
    public async Task Handle_SetsDismissedStatusAndRebuttalText()
    {
        var ws = await CreateWorkspaceAsync();
        var finding = await SeedFindingAsync(ws);

        var sut = new DismissFindingCommandHandler(_factory);
        await sut.Handle(new DismissFindingCommand(finding.Id, "Not applicable here."), default);

        var reloaded = await _db.Findings.AsNoTracking().SingleAsync(f => f.Id == finding.Id);
        reloaded.Status.Should().Be("dismissed");
        reloaded.RebuttalText.Should().Be("Not applicable here.");
    }

    [Fact]
    public async Task Handle_TrimsRebuttalText()
    {
        var ws = await CreateWorkspaceAsync();
        var finding = await SeedFindingAsync(ws);

        var sut = new DismissFindingCommandHandler(_factory);
        await sut.Handle(new DismissFindingCommand(finding.Id, "  trimmed rebuttal  "), default);

        var reloaded = await _db.Findings.AsNoTracking().SingleAsync(f => f.Id == finding.Id);
        reloaded.RebuttalText.Should().Be("trimmed rebuttal");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceRebuttal_Throws(string rebuttal)
    {
        var ws = await CreateWorkspaceAsync();
        var finding = await SeedFindingAsync(ws);

        var sut = new DismissFindingCommandHandler(_factory);
        var act = () => sut.Handle(new DismissFindingCommand(finding.Id, rebuttal), default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_UnknownFinding_Throws()
    {
        var sut = new DismissFindingCommandHandler(_factory);
        var act = () => sut.Handle(new DismissFindingCommand(Guid.NewGuid(), "Some rebuttal."), default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
