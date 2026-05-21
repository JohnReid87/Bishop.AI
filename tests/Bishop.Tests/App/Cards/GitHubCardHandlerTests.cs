using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.GitHub;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.App.Cards;

public sealed class GitHubCardHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IGhCli _ghCli;

    public GitHubCardHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _ghCli = Substitute.For<IGhCli>();
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(Workspace workspace, IReadOnlyList<Lane> lanes)> CreateWorkspaceWithLanesAsync(string? gitHubRepo = null)
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        if (gitHubRepo is not null)
        {
            workspace.GitHubRepo = gitHubRepo;
            await _db.SaveChangesAsync();
        }
        var lanes = await new ListLanesByWorkspaceQueryHandler(_db)
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return (workspace, lanes);
    }

    // ── CloseCard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseCard_SetsIsClosedTrue_AndPersists()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        var handler = new CloseCardCommandHandler(_db, _ghCli);

        // Act
        var result = await handler.Handle(new CloseCardCommand(card.Id), default);

        // Assert
        result.IsClosed.Should().BeTrue();
        (await _db.Cards.FindAsync(card.Id))!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task CloseCard_CardNotFound_Throws()
    {
        // Arrange
        var handler = new CloseCardCommandHandler(_db, _ghCli);

        // Act
        var act = async () => await handler.Handle(new CloseCardCommand(Guid.NewGuid()), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CloseCard_WithGitHubIssueAndRepo_CallsGhCli()
    {
        // Arrange
        const string repo = "owner/repo";
        var (_, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        card.GitHubIssueNumber = 77;
        await _db.SaveChangesAsync();
        var handler = new CloseCardCommandHandler(_db, _ghCli);

        // Act
        await handler.Handle(new CloseCardCommand(card.Id), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a => a[0] == "issue" && a[1] == "close" && a[2] == "77"),
            Arg.Any<CancellationToken>());
    }

    // ── ReopenCard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReopenCard_SetsIsClosedFalse_AndPersists()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        card.IsClosed = true;
        await _db.SaveChangesAsync();
        var handler = new ReopenCardCommandHandler(_db, _ghCli);

        // Act
        var result = await handler.Handle(new ReopenCardCommand(card.Id), default);

        // Assert
        result.IsClosed.Should().BeFalse();
        (await _db.Cards.FindAsync(card.Id))!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task ReopenCard_CardNotFound_Throws()
    {
        // Arrange
        var handler = new ReopenCardCommandHandler(_db, _ghCli);

        // Act
        var act = async () => await handler.Handle(new ReopenCardCommand(Guid.NewGuid()), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReopenCard_WithGitHubIssueAndRepo_CallsGhCli()
    {
        // Arrange
        const string repo = "owner/repo";
        var (_, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        card.GitHubIssueNumber = 55;
        card.IsClosed = true;
        await _db.SaveChangesAsync();
        var handler = new ReopenCardCommandHandler(_db, _ghCli);

        // Act
        await handler.Handle(new ReopenCardCommand(card.Id), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a => a[0] == "issue" && a[1] == "reopen" && a[2] == "55"),
            Arg.Any<CancellationToken>());
    }

    // ── PushCard ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PushCard_CardNotFound_Throws()
    {
        // Arrange
        var handler = new PushCardCommandHandler(_db, _ghCli);

        // Act
        var act = async () => await handler.Handle(new PushCardCommand(Guid.NewGuid()), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PushCard_NoGitHubRepo_Throws()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        var handler = new PushCardCommandHandler(_db, _ghCli);

        // Act
        var act = async () => await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no GitHub repo configured*");
    }

    [Fact]
    public async Task PushCard_AlreadyLinked_Throws()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: "owner/repo");
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        card.GitHubIssueNumber = 10;
        await _db.SaveChangesAsync();
        var handler = new PushCardCommandHandler(_db, _ghCli);

        // Act
        var act = async () => await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already linked*");
    }

    [Fact]
    public async Task PushCard_SetsGitHubIssueNumberAndPushedAt()
    {
        // Arrange
        const string repo = "owner/repo";
        var (_, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "My feature"), default);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/42"));
        var before = DateTimeOffset.UtcNow;
        var handler = new PushCardCommandHandler(_db, _ghCli);

        // Act
        var result = await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert
        result.GitHubIssueNumber.Should().Be(42);
        result.GitHubPushedAt.Should().NotBeNull().And.BeOnOrAfter(before);
        (await _db.Cards.FindAsync(card.Id))!.GitHubIssueNumber.Should().Be(42);
    }

    [Fact]
    public async Task PushCard_IssueBody_FooterContainsBishopCardNumberWithoutHash()
    {
        // Arrange
        const string repo = "owner/repo";
        var (_, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/1"));
        var handler = new PushCardCommandHandler(_db, _ghCli);

        // Act
        await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert
        await _ghCli.Received(1).RunCaptureAsync(
            Arg.Is<string[]>(a => a[6] == "--body" && a[7].Contains($"Bishop card {card.Number}") && !a[7].Contains($"Bishop card #{card.Number}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushCard_WithTags_CreatesLabelsForEachTag()
    {
        // Arrange
        const string repo = "owner/repo";
        var (_, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task", TagNames: ["feature", "urgent"]), default);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/7"));
        var handler = new PushCardCommandHandler(_db, _ghCli);

        // Act
        await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert
        await _ghCli.Received().RunAsync(
            Arg.Is<string[]>(a => a[0] == "label" && a[1] == "create" && a[2] == "feature"),
            Arg.Any<CancellationToken>());
        await _ghCli.Received().RunAsync(
            Arg.Is<string[]>(a => a[0] == "label" && a[1] == "create" && a[2] == "urgent"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushCard_ClosedCard_ClosesIssueOnGitHub()
    {
        // Arrange
        const string repo = "owner/repo";
        var (_, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Closed task"), default);
        card.IsClosed = true;
        await _db.SaveChangesAsync();
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/99"));
        var handler = new PushCardCommandHandler(_db, _ghCli);

        // Act
        await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a => a[0] == "issue" && a[1] == "close" && a[2] == "99"),
            Arg.Any<CancellationToken>());
    }

    // ── MoveCard → Done-lane transitions ─────────────────────────────────────

    private ISender CreateForwardingSender()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CloseCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => new CloseCardCommandHandler(_db, _ghCli)
                .Handle(ci.Arg<CloseCardCommand>(), ci.Arg<CancellationToken>()));
        sender.Send(Arg.Any<ReopenCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ReopenCardCommandHandler(_db, _ghCli)
                .Handle(ci.Arg<ReopenCardCommand>(), ci.Arg<CancellationToken>()));
        return sender;
    }

    [Fact]
    public async Task MoveCard_ToDoneLane_SetsIsClosedTrue()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        var handler = new MoveCardCommandHandler(_db, CreateForwardingSender());

        // Act
        await handler.Handle(new MoveCardCommand(card.Id, lanes[2].Id, 1), default);

        // Assert
        (await _db.Cards.FindAsync(card.Id))!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task MoveCard_ToDoneLane_WithKeepOpen_LeavesCardOpen()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        var sender = Substitute.For<ISender>();
        var handler = new MoveCardCommandHandler(_db, sender);

        // Act
        await handler.Handle(new MoveCardCommand(card.Id, lanes[2].Id, 1, KeepOpen: true), default);

        // Assert
        var stored = (await _db.Cards.FindAsync(card.Id))!;
        stored.IsClosed.Should().BeFalse();
        stored.LaneId.Should().Be(lanes[2].Id);
        await sender.DidNotReceive().Send(Arg.Any<CloseCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveCard_ToDoneLane_WithKeepOpenAndGitHubIssue_DoesNotCloseIssue()
    {
        // Arrange
        const string repo = "owner/repo";
        var (_, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        card.GitHubIssueNumber = 33;
        await _db.SaveChangesAsync();
        var handler = new MoveCardCommandHandler(_db, CreateForwardingSender());

        // Act
        await handler.Handle(new MoveCardCommand(card.Id, lanes[2].Id, 1, KeepOpen: true), default);

        // Assert
        await _ghCli.DidNotReceive().RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        (await _db.Cards.FindAsync(card.Id))!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task MoveCard_ToDoneLane_WithGitHubIssueAndRepo_ClosesIssue()
    {
        // Arrange
        const string repo = "owner/repo";
        var (_, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        card.GitHubIssueNumber = 11;
        await _db.SaveChangesAsync();
        var handler = new MoveCardCommandHandler(_db, CreateForwardingSender());

        // Act
        await handler.Handle(new MoveCardCommand(card.Id, lanes[2].Id, 1), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a => a[0] == "issue" && a[1] == "close" && a[2] == "11"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveCard_FromDoneLane_SetsIsClosedFalse()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[2].Id, "Task"), default);
        card.IsClosed = true;
        await _db.SaveChangesAsync();
        var handler = new MoveCardCommandHandler(_db, CreateForwardingSender());

        // Act
        await handler.Handle(new MoveCardCommand(card.Id, lanes[1].Id, 1), default);

        // Assert
        (await _db.Cards.FindAsync(card.Id))!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task MoveCard_FromDoneLane_WithGitHubIssueAndRepo_ReopensIssue()
    {
        // Arrange
        const string repo = "owner/repo";
        var (_, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[2].Id, "Task"), default);
        card.GitHubIssueNumber = 22;
        card.IsClosed = true;
        await _db.SaveChangesAsync();
        var handler = new MoveCardCommandHandler(_db, CreateForwardingSender());

        // Act
        await handler.Handle(new MoveCardCommand(card.Id, lanes[1].Id, 1), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a => a[0] == "issue" && a[1] == "reopen" && a[2] == "22"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveCard_WithinDoneLane_PreservesCloseState()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_db);
        var a = await add.Handle(new AddCardCommand(lanes[2].Id, "A"), default);
        var b = await add.Handle(new AddCardCommand(lanes[2].Id, "B"), default);
        a.IsClosed = true;
        b.IsClosed = true;
        await _db.SaveChangesAsync();
        var handler = new MoveCardCommandHandler(_db, CreateForwardingSender());

        // Act — reorder within Done; no close/reopen should be dispatched
        await handler.Handle(new MoveCardCommand(a.Id, lanes[2].Id, 1), default);

        // Assert
        await _ghCli.DidNotReceive().RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        (await _db.Cards.FindAsync(a.Id))!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task MoveCard_BetweenNonDoneLanes_DoesNotChangeCloseState()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        var handler = new MoveCardCommandHandler(_db, CreateForwardingSender());

        // Act — To Do → Doing
        await handler.Handle(new MoveCardCommand(card.Id, lanes[1].Id, 1), default);

        // Assert
        (await _db.Cards.FindAsync(card.Id))!.IsClosed.Should().BeFalse();
        await _ghCli.DidNotReceive().RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }
}
