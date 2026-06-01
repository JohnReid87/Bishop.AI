using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.ImportFromGitHub;
using Bishop.App.Services.GitHub;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Cards;

public sealed class ImportFromGitHubHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly IGhCli _ghCli;

    public ImportFromGitHubHandlerTests(DbFixture fixture)
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
        }
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return (workspace, lanes);
    }

    private static string IssueListJson(params (int number, string title, string body, string[] labels)[] issues)
    {
        var items = issues.Select(i =>
        {
            var labels = string.Join(",", i.labels.Select(l => $"{{\"name\":\"{l}\"}}"));
            return $"{{\"number\":{i.number},\"title\":\"{i.title}\",\"body\":\"{i.body}\",\"labels\":[{labels}]}}";
        });
        return $"[{string.Join(",", items)}]";
    }

    private ImportFromGitHubCommandHandler CreateHandler() =>
        new(_factory, _ghCli);

    // ── Missing GitHubRepo ───────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_MissingGitHubRepo_Throws()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceAsync();
        var handler = CreateHandler();

        // Act
        var act = async () =>
            await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no GitHub repo configured*");
    }

    // ── Success path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_Success_CreatesCardsInBacklogSortedByIssueNumberAsc()
    {
        // Arrange — issues returned out of order; should land sorted ASC
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceAsync(gitHubRepo: repo);
        var backlogLane = lanes.Single(l => l.Name == "Backlog");

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((5, "Fifth", "body5", []), (1, "First", "body1", []), (3, "Third", "body3", [])));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert
        result.Imported.Should().HaveCount(3);
        result.SkippedAlreadyPresent.Should().BeEmpty();
        result.Failed.Should().BeEmpty();

        // Cards in Backlog, in issue-number order
        var cards = await _db.Cards
            .Where(c => c.WorkspaceId == workspace.Id && c.LaneName == backlogLane.Name)
            .OrderBy(c => c.Position)
            .ToListAsync();
        cards.Should().HaveCount(3);
        cards.Select(c => c.GitHubIssueNumber).Should().Equal(1, 3, 5);
        cards.Select(c => c.Title).Should().Equal("First", "Third", "Fifth");
    }

    [Fact]
    public async Task ImportFromGitHub_SetsGitHubIssueNumberOnCard()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((42, "The answer", "body", [])));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert
        result.Imported.Should().HaveCount(1);
        result.Imported[0].GitHubIssueNumber.Should().Be(42);
        (await _db.Cards.FindAsync(result.Imported[0].Id))!.GitHubIssueNumber.Should().Be(42);
    }

    [Fact]
    public async Task ImportFromGitHub_DescriptionIncludesBodyAndFooter()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((7, "Title", "Issue body text", [])));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert
        var description = result.Imported[0].Description;
        description.Should().StartWith("Issue body text");
        description.Should().Contain("Imported from GitHub issue #7 (owner/repo)");
    }

    [Fact]
    public async Task ImportFromGitHub_EmptyBody_DescriptionIsFooterOnly()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((9, "No body", "", [])));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert
        var description = result.Imported[0].Description;
        description.Should().NotStartWith("\n");
        description.Should().StartWith("---");
        description.Should().Contain("Imported from GitHub issue #9 (owner/repo)");
    }

    // ── Dedupe ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_DedupeOnRerun_SkipsAlreadyPresent()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((1, "Issue One", "", []), (2, "Issue Two", "", [])));

        var handler = CreateHandler();
        var cmd = new ImportFromGitHubCommand(workspace.Id, null, 100, false);

        // First run
        var first = await handler.Handle(cmd, default);
        first.Imported.Should().HaveCount(2);

        // Act — second run
        var second = await handler.Handle(cmd, default);

        // Assert
        second.Imported.Should().BeEmpty();
        second.SkippedAlreadyPresent.Should().BeEquivalentTo([1, 2]);
    }

    // ── Label filtering ──────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_LabelFiltering_OnlyMatchingBrandTagsAttached()
    {
        // Arrange — brand palette includes "bug" and "feature"; issue carries labels
        // "bug" and "wontfix". Only "bug" is a brand tag so it is the only attached tag.
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((10, "A bug", "body", ["bug", "wontfix"])));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert
        result.Imported.Should().HaveCount(1);
        var card = await _db.Cards
            .FirstAsync(c => c.Id == result.Imported[0].Id);
        card.TagName.Should().Be("bug");
    }

    [Fact]
    public async Task ImportFromGitHub_LabelFiltering_NoMatchingTags_CardCreatedWithNoTags()
    {
        // Arrange — issue has labels but none match workspace tags
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((11, "Issue", "body", ["wontfix", "help wanted"])));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert
        result.Imported.Should().HaveCount(1);
        var card = await _db.Cards
            .FirstAsync(c => c.Id == result.Imported[0].Id);
        card.TagName.Should().BeNull();
    }

    // ── Dry-run ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_DryRun_WritesNothingToDatabase()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, lanes) = await CreateWorkspaceAsync(gitHubRepo: repo);
        var backlogLane = lanes.Single(l => l.Name == "Backlog");

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((20, "Preview me", "body", []), (21, "Me too", "", [])));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, true), default);

        // Assert — result shows would-be imports
        result.Imported.Should().HaveCount(2);
        result.Imported.Select(c => c.GitHubIssueNumber).Should().BeEquivalentTo([20, 21]);
        result.Failed.Should().BeEmpty();

        // No cards actually written
        var cardCount = await _db.Cards.CountAsync(c => c.WorkspaceId == workspace.Id && c.LaneName == backlogLane.Name);
        cardCount.Should().Be(0);
    }

    [Fact]
    public async Task ImportFromGitHub_DryRun_AlreadyPresentIssuesReportedAsSkipped()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((30, "Issue", "body", [])));

        var handler = CreateHandler();

        // Import once for real
        await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Act — dry-run
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, true), default);

        // Assert
        result.Imported.Should().BeEmpty();
        result.SkippedAlreadyPresent.Should().ContainSingle().Which.Should().Be(30);
    }

    // ── Atomicity ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_SaveFailure_RollsBackAll_NoCardsImported_NextCardNumberUnchanged()
    {
        // Arrange — pre-create a card whose Number equals the workspace's NextCardNumber,
        // so the second card the import assigns (NextCardNumber + 1) is fine, but the
        // first card collides on the unique (WorkspaceId, Number) index when SaveChanges
        // runs. With atomic semantics, neither card is persisted and NextCardNumber is
        // unchanged after rollback.
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);
        var initialNextCardNumber = workspace.NextCardNumber;

        _db.Cards.Add(new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            LaneName = SystemLaneNames.Backlog,
            Title = "Pre-existing collider",
            Description = string.Empty,
            Number = initialNextCardNumber,
            Position = 1,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((200, "First", "", []), (201, "Second", "", [])));

        var handler = CreateHandler();

        // Act
        var act = async () =>
            await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert — save threw and the entire transaction rolled back
        await act.Should().ThrowAsync<DbUpdateException>();

        var importedIssueNumbers = await _db.Cards
            .Where(c => c.WorkspaceId == workspace.Id && c.GitHubIssueNumber.HasValue)
            .Select(c => c.GitHubIssueNumber!.Value)
            .ToListAsync();
        importedIssueNumbers.Should().BeEmpty();

        _db.ChangeTracker.Clear();
        var wsAfter = await _db.Workspaces.FindAsync(workspace.Id);
        wsAfter!.NextCardNumber.Should().Be(initialNextCardNumber);
    }

    // ── Missing workspace ─────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_MissingWorkspace_Throws()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var act = async () =>
            await handler.Handle(new ImportFromGitHubCommand(Guid.NewGuid(), null, 100, false), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ── Label filter forwarded to gh CLI ──────────────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_LabelFilter_ForwardedToGhCli()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        string[]? capturedArgs = null;
        _ghCli.RunCaptureAsync(
                Arg.Do<string[]>(args => capturedArgs = args),
                Arg.Any<CancellationToken>())
            .Returns(IssueListJson());

        var handler = CreateHandler();

        // Act
        await handler.Handle(new ImportFromGitHubCommand(workspace.Id, "bug", 100, false), default);

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs.Should().Contain("--label");
        capturedArgs.Should().Contain("bug");
    }

    // ── Dry-run: skip check isolated to workspace ─────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_DryRun_SkipCheckIsolatedToWorkspace()
    {
        // Arrange — import issue 30 into workspace A and issue 99 into workspace B.
        // A dry-run on workspace A must only report 30 as skipped, not 99 (which
        // lives in a different workspace). An AND→OR mutation on the WHERE clause
        // at line 56 would include workspace B's cards and wrongly skip issue 99.
        const string repo = "owner/repo";
        var (workspaceA, _) = await CreateWorkspaceAsync(gitHubRepo: repo);
        var (workspaceB, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        var handler = CreateHandler();

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((30, "Issue", "body", [])));
        await handler.Handle(new ImportFromGitHubCommand(workspaceA.Id, null, 100, false), default);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((99, "Other issue", "body", [])));
        await handler.Handle(new ImportFromGitHubCommand(workspaceB.Id, null, 100, false), default);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((30, "Issue", "body", []), (99, "Other issue", "body", [])));

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspaceA.Id, null, 100, true), default);

        // Assert — issue 30 already in workspace A; issue 99 is only in workspace B and must not be skipped
        result.SkippedAlreadyPresent.Should().Equal([30]);
        result.Imported.Should().HaveCount(1);
        result.Imported[0].GitHubIssueNumber.Should().Be(99);
    }

    // ── Position calculation with pre-populated backlog ───────────────────────

    [Fact]
    public async Task ImportFromGitHub_PositionCalculation_AppendsAfterExistingBacklogCards()
    {
        // Arrange — import two cards first so the backlog is not empty
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        var handler = CreateHandler();

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((50, "First", "body", []), (51, "Second", "body", [])));
        await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((52, "Third", "body", [])));

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert — new card appends at position 3 (max existing position 2, plus 1)
        result.Imported.Should().HaveCount(1);
        var importedCard = await _db.Cards.FindAsync(result.Imported[0].Id);
        importedCard!.Position.Should().Be(3);
    }

    // ── Card number sequence ──────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_CardNumbersAssignedSequentially()
    {
        // Arrange
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);
        var initialNextCardNumber = workspace.NextCardNumber;

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((60, "Issue A", "body", []), (61, "Issue B", "body", [])));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert — cards receive consecutive numbers from the initial value; workspace counter advances
        result.Imported.Should().HaveCount(2);
        result.Imported[0].Number.Should().Be(initialNextCardNumber);
        result.Imported[1].Number.Should().Be(initialNextCardNumber + 1);

        _db.ChangeTracker.Clear();
        var wsAfter = await _db.Workspaces.FindAsync(workspace.Id);
        wsAfter!.NextCardNumber.Should().Be(initialNextCardNumber + 2);
    }

}
