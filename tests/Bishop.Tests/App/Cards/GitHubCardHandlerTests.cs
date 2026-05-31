using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Services.GitHub;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Bishop.Tests.App.Cards;

public sealed class GitHubCardHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly IGhCli _ghCli;

    public GitHubCardHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _ghCli = Substitute.For<IGhCli>();
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    // Persists test-set fields of a Card created via the factory. Re-fetches
    // the row via _db so we don't overwrite columns the test didn't intend to
    // touch (e.g. Position, which shifts as sibling cards are added).
    private async Task PersistMutationAsync(Card card)
    {
        var tracked = await _db.Cards.FindAsync(card.Id)
            ?? throw new InvalidOperationException($"Card {card.Id} not found.");
        tracked.IsClosed = card.IsClosed;
        tracked.GitHubIssueNumber = card.GitHubIssueNumber;
        tracked.GitHubPushedAt = card.GitHubPushedAt;
        tracked.Description = card.Description;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    // Same for Workspace, used to set GitHubRepo on a workspace the factory created.
    private async Task PersistMutationAsync(Workspace workspace)
    {
        var tracked = await _db.Workspaces.FindAsync(workspace.Id)
            ?? throw new InvalidOperationException($"Workspace {workspace.Id} not found.");
        tracked.GitHubRepo = workspace.GitHubRepo;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task<(Workspace workspace, IReadOnlyList<LaneInfo> lanes)> CreateWorkspaceWithLanesAsync(string? gitHubRepo = null)
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        if (gitHubRepo is not null)
        {
            workspace.GitHubRepo = gitHubRepo;
            await PersistMutationAsync(workspace);
        }
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return (workspace, lanes);
    }

    // ── CloseCard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseCard_SetsIsClosedTrue_AndPersists()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name,"Task"), default);
        var handler = new CloseCardCommandHandler(_factory, _ghCli);

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
        var handler = new CloseCardCommandHandler(_factory, _ghCli);

        // Act
        var act = async () => await handler.Handle(new CloseCardCommand(Guid.NewGuid()), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task CloseCard_WithGitHubIssueAndRepo_CallsGhCli()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name,"Task"), default);
        card.GitHubIssueNumber = 77;
        await PersistMutationAsync(card);
        var handler = new CloseCardCommandHandler(_factory, _ghCli);

        // Act
        await handler.Handle(new CloseCardCommand(card.Id), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a => a[0] == "issue" && a[1] == "close" && a[2] == "77" && a[3] == "--repo" && a[4] == repo),
            Arg.Any<CancellationToken>());
    }

    // ── ReopenCard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReopenCard_SetsIsClosedFalse_AndPersists()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name,"Task"), default);
        card.IsClosed = true;
        await PersistMutationAsync(card);
        var handler = new ReopenCardCommandHandler(_factory, _ghCli);

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
        var handler = new ReopenCardCommandHandler(_factory, _ghCli);

        // Act
        var act = async () => await handler.Handle(new ReopenCardCommand(Guid.NewGuid()), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task ReopenCard_WithGitHubIssueAndRepo_CallsGhCli()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name,"Task"), default);
        card.GitHubIssueNumber = 55;
        card.IsClosed = true;
        await PersistMutationAsync(card);
        var handler = new ReopenCardCommandHandler(_factory, _ghCli);

        // Act
        await handler.Handle(new ReopenCardCommand(card.Id), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a => a[0] == "issue" && a[1] == "reopen" && a[2] == "55" && a[3] == "--repo" && a[4] == repo),
            Arg.Any<CancellationToken>());
    }

    // ── PushCard ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PushCard_CardNotFound_Throws()
    {
        // Arrange
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

        // Act
        var act = async () => await handler.Handle(new PushCardCommand(Guid.NewGuid()), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task PushCard_NoGitHubRepo_Throws()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name,"Task"), default);
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: "owner/repo");
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name,"Task"), default);
        card.GitHubIssueNumber = 10;
        await PersistMutationAsync(card);
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name,"My feature"), default);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/42"));
        var before = DateTimeOffset.UtcNow;
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

        // Act
        var result = await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert
        result.GitHubIssueNumber.Should().Be(42);
        result.GitHubPushedAt.Should().NotBeNull().And.BeOnOrAfter(before);
        (await _db.Cards.FindAsync(card.Id))!.GitHubIssueNumber.Should().Be(42);
        await _ghCli.Received(1).RunCaptureAsync(
            Arg.Is<string[]>(a => a[0] == "issue" && a[1] == "create" && a.Contains("--repo")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushCard_IssueBody_FooterContainsBishopCardNumberWithoutHash()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name,"Task"), default);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/1"));
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

        // Act
        await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert
        await _ghCli.Received(1).RunCaptureAsync(
            Arg.Is<string[]>(a => a[6] == "--body" && a[7].Contains($"Bishop card {card.Number}") && !a[7].Contains($"Bishop card #{card.Number}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushCard_WithTag_CreatesLabelOnGitHub()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name,"Task", TagName: "feature"), default);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/7"));
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

        // Act
        await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a =>
                a[0] == "label" && a[1] == "create" && a[2] == "feature"
                && a.Contains("--color") && a.Contains("--repo") && a.Contains("--force")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushCard_ClosedCard_ClosesIssueOnGitHub()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name,"Closed task"), default);
        card.IsClosed = true;
        await PersistMutationAsync(card);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/99"));
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

        // Act
        await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a =>
                a[0] == "issue" && a[1] == "close" && a[2] == "99" && a.Contains("--repo")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushCard_WithDescription_IncludesDescriptionInBody()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
        card.Description = "Has body text";
        await PersistMutationAsync(card);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/1"));
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

        // Act
        await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert — body starts with the description text (not the "---" separator)
        await _ghCli.Received(1).RunCaptureAsync(
            Arg.Is<string[]>(a => a[6] == "--body" && a[7].StartsWith("Has body text")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushCard_EmptyDescription_TreatsAsEmpty()
    {
        // Arrange — AddCardCommand defaults Description to ""
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/1"));
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

        // Act
        await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert — body starts with the "---" separator (no description line)
        await _ghCli.Received(1).RunCaptureAsync(
            Arg.Is<string[]>(a => a[6] == "--body" && a[7].StartsWith("---")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushCard_WhitespaceDescription_TreatsAsEmpty()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
        card.Description = "   \t\n  ";
        await PersistMutationAsync(card);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/1"));
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

        // Act
        await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert — whitespace-only description is treated the same as empty
        await _ghCli.Received(1).RunCaptureAsync(
            Arg.Is<string[]>(a => a[6] == "--body" && a[7].StartsWith("---")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushCard_WithTag_AddsLabelToIssue()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task", TagName: "feature"), default);
        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://github.com/owner/repo/issues/7"));
        var handler = new PushCardCommandHandler(_factory, _ghCli, TimeProvider.System);

        // Act
        await handler.Handle(new PushCardCommand(card.Id), default);

        // Assert — issue create args include `--label feature`
        await _ghCli.Received(1).RunCaptureAsync(
            Arg.Is<string[]>(a =>
                a[0] == "issue" && a[1] == "create"
                && Array.IndexOf(a, "--label") >= 0
                && a[Array.IndexOf(a, "--label") + 1] == "feature"),
            Arg.Any<CancellationToken>());
    }

    // ── MoveCard → Done-lane transitions ─────────────────────────────────────

    private MoveCardCommandHandler MoveHandler() =>
        new(_factory, _ghCli, NullLogger<MoveCardCommandHandler>.Instance);

    [Fact]
    public async Task MoveCard_ToDoneLane_SetsIsClosedTrue()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);

        // Act
        await MoveHandler().Handle(new MoveCardCommand(card.Id, lanes[3].Name, 1), default);

        // Assert
        (await _db.Cards.FindAsync(card.Id))!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task MoveCard_ToDoneLane_WithKeepOpen_LeavesCardOpen()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);

        // Act
        await MoveHandler().Handle(new MoveCardCommand(card.Id, lanes[3].Name, 1, KeepOpen: true), default);

        // Assert
        var stored = (await _db.Cards.FindAsync(card.Id))!;
        stored.IsClosed.Should().BeFalse();
        stored.LaneName.Should().Be(lanes[3].Name);
        await _ghCli.DidNotReceive().RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveCard_ToDoneLane_WithKeepOpenAndGitHubIssue_DoesNotCloseIssue()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
        card.GitHubIssueNumber = 33;
        await PersistMutationAsync(card);

        // Act
        await MoveHandler().Handle(new MoveCardCommand(card.Id, lanes[3].Name, 1, KeepOpen: true), default);

        // Assert
        await _ghCli.DidNotReceive().RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        (await _db.Cards.FindAsync(card.Id))!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task MoveCard_ToDoneLane_WithGitHubIssueAndRepo_ClosesIssue()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
        card.GitHubIssueNumber = 11;
        await PersistMutationAsync(card);

        // Act
        await MoveHandler().Handle(new MoveCardCommand(card.Id, lanes[3].Name, 1), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a =>
                a[0] == "issue" && a[1] == "close" && a[2] == "11"
                && Array.IndexOf(a, "--repo") >= 0
                && a[Array.IndexOf(a, "--repo") + 1] == repo),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveCard_ToDoneLane_GitHubFailure_DbStateRemainsConsistent()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
        card.GitHubIssueNumber = 99;
        await PersistMutationAsync(card);
        _ghCli.RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("GitHub unavailable")));

        // Act — GitHub failure must not propagate
        await MoveHandler().Handle(new MoveCardCommand(card.Id, lanes[3].Name, 1), default);

        // Assert — move and close committed atomically; GitHub failure does not undo them
        var stored = (await _db.Cards.FindAsync(card.Id))!;
        stored.LaneName.Should().Be(lanes[3].Name);
        stored.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task MoveCard_FromDoneLane_SetsIsClosedFalse()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[3].Name, "Task"), default);
        card.IsClosed = true;
        await PersistMutationAsync(card);

        // Act
        await MoveHandler().Handle(new MoveCardCommand(card.Id, lanes[2].Name, 1), default);

        // Assert
        (await _db.Cards.FindAsync(card.Id))!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task MoveCard_FromDoneLane_WithGitHubIssueAndRepo_ReopensIssue()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[3].Name, "Task"), default);
        card.GitHubIssueNumber = 22;
        card.IsClosed = true;
        await PersistMutationAsync(card);

        // Act
        await MoveHandler().Handle(new MoveCardCommand(card.Id, lanes[2].Name, 1), default);

        // Assert
        await _ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(a =>
                a[0] == "issue" && a[1] == "reopen" && a[2] == "22"
                && Array.IndexOf(a, "--repo") >= 0
                && a[Array.IndexOf(a, "--repo") + 1] == repo),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveCard_FromDoneLane_GitHubFailure_DbStateRemainsConsistent()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[3].Name, "Task"), default);
        card.GitHubIssueNumber = 88;
        card.IsClosed = true;
        await PersistMutationAsync(card);
        _ghCli.RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("GitHub unavailable")));

        // Act — GitHub failure must not propagate
        await MoveHandler().Handle(new MoveCardCommand(card.Id, lanes[2].Name, 1), default);

        // Assert — move and reopen committed atomically; GitHub failure does not undo them
        var stored = (await _db.Cards.FindAsync(card.Id))!;
        stored.LaneName.Should().Be(lanes[2].Name);
        stored.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task MoveCard_WithinDoneLane_PreservesCloseState()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);
        var a = await add.Handle(new AddCardCommand(workspace.Id, lanes[3].Name, "A"), default);
        var b = await add.Handle(new AddCardCommand(workspace.Id, lanes[3].Name, "B"), default);
        a.IsClosed = true;
        b.IsClosed = true;
        await PersistMutationAsync(a);
        await PersistMutationAsync(b);

        // Act — reorder within Done; no close/reopen should fire
        await MoveHandler().Handle(new MoveCardCommand(a.Id, lanes[3].Name, 1), default);

        // Assert
        await _ghCli.DidNotReceive().RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        (await _db.Cards.FindAsync(a.Id))!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task MoveCard_BetweenNonDoneLanes_DoesNotChangeCloseState()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);

        // Act — To Do → Doing
        await MoveHandler().Handle(new MoveCardCommand(card.Id, lanes[1].Name, 1), default);

        // Assert
        (await _db.Cards.FindAsync(card.Id))!.IsClosed.Should().BeFalse();
        await _ghCli.DidNotReceive().RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveCard_WithinSameLane_WithGitHubIssueAndRepo_DoesNotCallGitHub()
    {
        // Arrange — within-lane reorder must not trigger close/reopen regardless of linked issue
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "A"), default);
        var b = await add.Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "B"), default);
        b.GitHubIssueNumber = 77;
        await PersistMutationAsync(b);

        // Act — reorder within the same lane
        await MoveHandler().Handle(new MoveCardCommand(b.Id, lanes[0].Name, 2), default);

        // Assert — no GitHub call; card not closed
        await _ghCli.DidNotReceive().RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        (await _db.Cards.FindAsync(b.Id))!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task MoveCard_WithinDoneLane_WithGitHubIssueAndRepo_DoesNotReopenIssue()
    {
        // Arrange — reorder within Done must not set leavingDone, even with linked issue + repo
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, lanes[3].Name, "A"), default);
        var b = await add.Handle(new AddCardCommand(workspace.Id, lanes[3].Name, "B"), default);
        b.GitHubIssueNumber = 88;
        b.IsClosed = true;
        await PersistMutationAsync(b);

        // Act — within-Done reorder
        await MoveHandler().Handle(new MoveCardCommand(b.Id, lanes[3].Name, 2), default);

        // Assert — no reopen call; IsClosed unchanged
        await _ghCli.DidNotReceive().RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        (await _db.Cards.FindAsync(b.Id))!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task MoveCard_ToDoneLane_GitHubCloseFailure_LogsWarning()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
        card.GitHubIssueNumber = 55;
        await PersistMutationAsync(card);
        _ghCli.RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("GitHub unavailable")));
        var logger = new CapturingLogger();
        var handler = new MoveCardCommandHandler(_factory, _ghCli, logger);

        // Act — GitHub close failure must be caught and logged, not rethrown
        await handler.Handle(new MoveCardCommand(card.Id, lanes[3].Name, 1), default);

        // Assert
        logger.WarningCount.Should().Be(1);
    }

    [Fact]
    public async Task MoveCard_FromDoneLane_GitHubReopenFailure_LogsWarning()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync(gitHubRepo: repo);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[3].Name, "Task"), default);
        card.GitHubIssueNumber = 66;
        card.IsClosed = true;
        await PersistMutationAsync(card);
        _ghCli.RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("GitHub unavailable")));
        var logger = new CapturingLogger();
        var handler = new MoveCardCommandHandler(_factory, _ghCli, logger);

        // Act — GitHub reopen failure must be caught and logged, not rethrown
        await handler.Handle(new MoveCardCommand(card.Id, lanes[2].Name, 1), default);

        // Assert
        logger.WarningCount.Should().Be(1);
    }

    private sealed class CapturingLogger : ILogger<MoveCardCommandHandler>
    {
        public int WarningCount { get; private set; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) WarningCount++;
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
