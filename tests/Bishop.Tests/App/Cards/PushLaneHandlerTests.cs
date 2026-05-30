using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.PushLane;
using Bishop.App.Services.GitHub;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bishop.Tests.App.Cards;

public sealed class PushLaneHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly IGhCli _ghCli;

    public PushLaneHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _ghCli = Substitute.For<IGhCli>();
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(Workspace workspace, IReadOnlyList<LaneInfo> lanes)> CreateWorkspaceAsync(string? gitHubRepo = null)
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        if (gitHubRepo is not null)
        {
            var tracked = await _db.Workspaces.FindAsync(workspace.Id)
                ?? throw new InvalidOperationException();
            tracked.GitHubRepo = gitHubRepo;
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
            workspace.GitHubRepo = gitHubRepo;
        }
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return (workspace, lanes);
    }

    private async Task PersistMutationAsync(Card card)
    {
        var tracked = await _db.Cards.FindAsync(card.Id)
            ?? throw new InvalidOperationException();
        tracked.GitHubIssueNumber = card.GitHubIssueNumber;
        tracked.GitHubPushedAt = card.GitHubPushedAt;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private ISender CreateForwardingSender()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System)
                .Handle(ci.Arg<PushCardCommand>(), ci.Arg<CancellationToken>()));
        return sender;
    }

    // ── empty lane ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PushLane_EmptyLane_ReturnsZeroCounts()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync(gitHubRepo: "owner/repo");
        var handler = new PushLaneCommandHandler(_factory, CreateForwardingSender());

        var result = await handler.Handle(new PushLaneCommand(workspace.Id, lanes[1].Name), default);

        result.Pushed.Should().BeEmpty();
        result.SkippedAlreadyLinked.Should().Be(0);
        result.Failed.Should().BeEmpty();
    }

    // ── all already linked ────────────────────────────────────────────────────

    [Fact]
    public async Task PushLane_AllAlreadyLinked_SkipsAllAndReturnsCount()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync(gitHubRepo: "owner/repo");
        var add = new AddCardCommandHandler(_factory);
        var a = await add.Handle(new AddCardCommand(workspace.Id, lanes[1].Name, "A"), default);
        var b = await add.Handle(new AddCardCommand(workspace.Id, lanes[1].Name, "B"), default);
        a.GitHubIssueNumber = 1;
        b.GitHubIssueNumber = 2;
        await PersistMutationAsync(a);
        await PersistMutationAsync(b);

        var sender = Substitute.For<ISender>();
        var handler = new PushLaneCommandHandler(_factory, sender);

        var result = await handler.Handle(new PushLaneCommand(workspace.Id, lanes[1].Name), default);

        result.Pushed.Should().BeEmpty();
        result.SkippedAlreadyLinked.Should().Be(2);
        result.Failed.Should().BeEmpty();
        await sender.DidNotReceive().Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>());
    }

    // ── mixed lane ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PushLane_MixedLane_PushesUnlinkedSkipsLinked()
    {
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceAsync(gitHubRepo: repo);
        var add = new AddCardCommandHandler(_factory);
        var linked = await add.Handle(new AddCardCommand(workspace.Id, lanes[1].Name, "Linked"), default);
        var unlinked = await add.Handle(new AddCardCommand(workspace.Id, lanes[1].Name, "Unlinked"), default);
        linked.GitHubIssueNumber = 5;
        await PersistMutationAsync(linked);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns("https://github.com/owner/repo/issues/42");

        var handler = new PushLaneCommandHandler(_factory, CreateForwardingSender());

        var result = await handler.Handle(new PushLaneCommand(workspace.Id, lanes[1].Name), default);

        result.Pushed.Should().HaveCount(1);
        result.Pushed[0].Number.Should().Be(unlinked.Number);
        result.SkippedAlreadyLinked.Should().Be(1);
        result.Failed.Should().BeEmpty();
    }

    // ── partial failure ───────────────────────────────────────────────────────

    [Fact]
    public async Task PushLane_PartialFailure_ContinuesBatchAndReportsFailures()
    {
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceAsync(gitHubRepo: repo);
        var add = new AddCardCommandHandler(_factory);
        var cardA = await add.Handle(new AddCardCommand(workspace.Id, lanes[1].Name, "A"), default);
        var cardB = await add.Handle(new AddCardCommand(workspace.Id, lanes[1].Name, "B"), default);

        _ghCli.RunCaptureAsync(
                Arg.Is<string[]>(a => a.Contains("--title") && Array.IndexOf(a, "--title") + 1 < a.Length && a[Array.IndexOf(a, "--title") + 1] == "A"),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("gh error"));
        _ghCli.RunCaptureAsync(
                Arg.Is<string[]>(a => a.Contains("--title") && Array.IndexOf(a, "--title") + 1 < a.Length && a[Array.IndexOf(a, "--title") + 1] == "B"),
                Arg.Any<CancellationToken>())
            .Returns("https://github.com/owner/repo/issues/99");

        var handler = new PushLaneCommandHandler(_factory, CreateForwardingSender());

        var result = await handler.Handle(new PushLaneCommand(workspace.Id, lanes[1].Name), default);

        result.Pushed.Should().HaveCount(1);
        result.Pushed[0].Number.Should().Be(cardB.Number);
        result.Failed.Should().HaveCount(1);
        result.Failed[0].CardNumber.Should().Be(cardA.Number);
        result.Failed[0].Error.Should().Contain("gh error");
    }

    // ── dry-run ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task PushLane_DryRun_ReturnsWouldPushWithoutCallingGh()
    {
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceAsync(gitHubRepo: repo);
        var add = new AddCardCommandHandler(_factory);
        var linked = await add.Handle(new AddCardCommand(workspace.Id, lanes[1].Name, "Linked"), default);
        var unlinked = await add.Handle(new AddCardCommand(workspace.Id, lanes[1].Name, "Unlinked"), default);
        linked.GitHubIssueNumber = 7;
        await PersistMutationAsync(linked);

        var sender = Substitute.For<ISender>();
        var handler = new PushLaneCommandHandler(_factory, sender);

        var result = await handler.Handle(new PushLaneCommand(workspace.Id, lanes[1].Name, DryRun: true), default);

        result.Pushed.Should().HaveCount(1);
        result.Pushed[0].Number.Should().Be(unlinked.Number);
        result.SkippedAlreadyLinked.Should().Be(1);
        result.Failed.Should().BeEmpty();
        await sender.DidNotReceive().Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>());
    }

    // ── missing GitHubRepo ────────────────────────────────────────────────────

    [Fact]
    public async Task PushLane_NoGitHubRepo_Throws()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var handler = new PushLaneCommandHandler(_factory, Substitute.For<ISender>());

        var act = async () => await handler.Handle(new PushLaneCommand(workspace.Id, lanes[1].Name), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no GitHub repo configured*");
    }

    // ── unknown lane name ─────────────────────────────────────────────────────

    [Fact]
    public async Task PushLane_UnknownLane_Throws()
    {
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: "owner/repo");
        var handler = new PushLaneCommandHandler(_factory, Substitute.For<ISender>());

        var act = async () => await handler.Handle(new PushLaneCommand(workspace.Id, "Nonexistent"), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}
