using Bishop.App.Findings.LinkFindingToCard;
using Bishop.App.Findings.RecordFindings;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Findings;

public sealed class LinkFindingToCardCommandHandlerTests : IClassFixture<DbFixture>, IDisposable
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly string _tempRoot;

    public LinkFindingToCardCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _tempRoot = Path.Combine(Path.GetTempPath(), "bishop-linkfinding-" + Guid.NewGuid().ToString("N"));
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
        return await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
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
    public async Task Handle_SetsCardedStatusAndLinkedCardId()
    {
        var ws = await CreateWorkspaceAsync();
        var finding = await SeedFindingAsync(ws);

        var sut = new LinkFindingToCardCommandHandler(_factory);
        await sut.Handle(new LinkFindingToCardCommand(finding.Id, 17), default);

        var reloaded = await _db.Findings.AsNoTracking().SingleAsync(f => f.Id == finding.Id);
        reloaded.Status.Should().Be("carded");
        reloaded.LinkedCardId.Should().Be(17);
    }

    [Fact]
    public async Task Handle_LinkSurvivesRerun()
    {
        var ws = await CreateWorkspaceAsync();
        var finding = await SeedFindingAsync(ws);

        await new LinkFindingToCardCommandHandler(_factory)
            .Handle(new LinkFindingToCardCommand(finding.Id, 42), default);

        // Re-record the same findings (e.g. user re-runs the skill).
        const string json = """
            { "findings": [ { "title": "T", "body": "b", "outcome": "parked",
                              "file": "src/F.cs", "rule": "R", "symbol": "S" } ] }
            """;
        await new RecordFindingsCommandHandler(_factory, TimeProvider.System)
            .Handle(new RecordFindingsCommand(ws.Id, ws.Path, "bish-arch", json, "sha2"), default);

        var reloaded = await _db.Findings.AsNoTracking().SingleAsync(f => f.Id == finding.Id);
        reloaded.Status.Should().Be("carded");
        reloaded.LinkedCardId.Should().Be(42);
    }

    [Fact]
    public async Task Handle_UnknownFinding_Throws()
    {
        var sut = new LinkFindingToCardCommandHandler(_factory);
        var act = () => sut.Handle(new LinkFindingToCardCommand(Guid.NewGuid(), 1), default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_NonPositiveCardNumber_Throws()
    {
        var ws = await CreateWorkspaceAsync();
        var finding = await SeedFindingAsync(ws);

        var sut = new LinkFindingToCardCommandHandler(_factory);
        var act = () => sut.Handle(new LinkFindingToCardCommand(finding.Id, 0), default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
