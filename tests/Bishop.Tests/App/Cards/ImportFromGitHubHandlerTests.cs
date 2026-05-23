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

    // ── Partial failure ──────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromGitHub_PartialFailure_ContinuesBatch_AndCollectsError()
    {
        // Arrange — two issues; DB factory throws on the second per-issue context creation
        const string repo = "owner/repo";
        var (workspace, _) = await CreateWorkspaceAsync(gitHubRepo: repo);

        _ghCli.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(IssueListJson((100, "First issue", "", []), (101, "Second issue", "", [])));

        // Wrap the real factory so that the third CreateDbContextAsync call (second issue) throws
        var failingFactory = new ThrowOnCallNFactory(_factory, failAtCall: 3);
        var handler = new ImportFromGitHubCommandHandler(failingFactory, _ghCli);

        // Act
        var result = await handler.Handle(new ImportFromGitHubCommand(workspace.Id, null, 100, false), default);

        // Assert — first issue imported, second failed
        result.Imported.Should().HaveCount(1);
        result.Imported[0].GitHubIssueNumber.Should().Be(100);
        result.Failed.Should().HaveCount(1);
        result.Failed[0].IssueNumber.Should().Be(101);
        result.SkippedAlreadyPresent.Should().BeEmpty();
    }

    private sealed class ThrowOnCallNFactory(IDbContextFactory<BishopDbContext> inner, int failAtCall)
        : IDbContextFactory<BishopDbContext>
    {
        private int _callCount;

        public BishopDbContext CreateDbContext() => inner.CreateDbContext();

        public Task<BishopDbContext> CreateDbContextAsync(CancellationToken cancellationToken)
        {
            _callCount++;
            if (_callCount == failAtCall)
                throw new InvalidOperationException("Simulated DB failure");
            return inner.CreateDbContextAsync(cancellationToken);
        }
    }
}
