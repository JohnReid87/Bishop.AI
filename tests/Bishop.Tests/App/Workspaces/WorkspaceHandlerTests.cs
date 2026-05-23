using Bishop.App.Git;
using Bishop.App.Lanes;
using Bishop.App.Tags;
using Bishop.App.Terminal;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.App.Workspaces.DeleteWorkspace;
using Bishop.App.Workspaces.GetWorkspace;
using Bishop.App.Workspaces.InitWorkspace;
using Bishop.App.Workspaces.LaunchPlainTerminal;
using Bishop.App.Workspaces.LaunchWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.ReorderWorkspaces;
using Bishop.App.Workspaces.SetWorkspaceGitHubRepo;
using Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;
using Bishop.App.Workspaces.UpdateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace Bishop.Tests.App.Workspaces;

public sealed class WorkspaceHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public WorkspaceHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private InitWorkspaceCommandHandler CreateInitHandler(IGitCli? git = null)
    {
        if (git is null)
        {
            var g = Substitute.For<IGitCli>();
            g.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
            return new InitWorkspaceCommandHandler(_factory, g);
        }
        return new InitWorkspaceCommandHandler(_factory, git);
    }

    [Fact]
    public async Task CreateWorkspace_PersistsAndReturnsWorkspace()
    {
        // Arrange
        var name = U("MyRepo");
        var handler = new CreateWorkspaceCommandHandler(_factory);

        // Act
        var result = await handler.Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be(name);
        result.Path.Should().Be($@"C:\code\{name}");
        result.CreatedAt.Should().NotBe(default);
        result.UpdatedAt.Should().Be(result.CreatedAt);
    }

    [Fact]
    public async Task ListWorkspaces_ReturnsOrderedByPosition()
    {
        // Arrange
        var betaName = U("Beta");
        var alphaName = U("Alpha");
        var create = new CreateWorkspaceCommandHandler(_factory);
        var beta = await create.Handle(new CreateWorkspaceCommand(betaName, $@"C:\{betaName}"), default);
        var alpha = await create.Handle(new CreateWorkspaceCommand(alphaName, $@"C:\{alphaName}"), default);
        var handler = new ListWorkspacesQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListWorkspacesQuery(), default);

        // Assert — Beta was created first so it has a lower position than Alpha
        var ours = result.Where(w => w.Id == beta.Id || w.Id == alpha.Id).ToList();
        ours.Should().HaveCount(2);
        ours[0].Name.Should().Be(betaName);
        ours[1].Name.Should().Be(alphaName);
    }

    [Fact]
    public async Task GetWorkspace_ReturnsCorrectWorkspace()
    {
        // Arrange
        var name = U("MyRepo");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new GetWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new GetWorkspaceQuery(created.Id), default);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(name);
    }

    [Fact]
    public async Task GetWorkspace_ReturnsNullForUnknownId()
    {
        // Arrange
        var handler = new GetWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new GetWorkspaceQuery(Guid.NewGuid()), default);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetWorkspace_IncludesLanesOrderedByPosition()
    {
        // Arrange
        var name = U("WithLanes");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var handler = new GetWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new GetWorkspaceQuery(created.Id), default);

        // Assert
        result.Should().NotBeNull();
        result!.Lanes.Should().HaveCount(4);
        result.Lanes.Select(l => l.Position).Should().BeInAscendingOrder();
        result.Lanes.Select(l => l.Name).Should().Equal("Backlog", "To Do", "Doing", "Done");
    }

    [Fact]
    public async Task UpdateWorkspace_ChangesNameAndPath()
    {
        // Arrange
        var oldName = U("Old");
        var newName = U("New");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(oldName, $@"C:\{oldName}"), default);
        var handler = new UpdateWorkspaceCommandHandler(_factory);

        // Act
        var result = await handler.Handle(
            new UpdateWorkspaceCommand(created.Id, newName, $@"C:\{newName}"), default);

        // Assert
        result.Name.Should().Be(newName);
        result.Path.Should().Be($@"C:\{newName}");
        result.UpdatedAt.Should().BeOnOrAfter(result.CreatedAt);
    }

    [Fact]
    public async Task DeleteWorkspace_RemovesFromDatabase()
    {
        // Arrange
        var name = U("ToDelete");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);

        // Act
        await new DeleteWorkspaceCommandHandler(_factory)
            .Handle(new DeleteWorkspaceCommand(created.Id), default);

        // Assert
        var remaining = await new ListWorkspacesQueryHandler(_factory)
            .Handle(new ListWorkspacesQuery(), default);
        remaining.Should().NotContain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task InitWorkspace_FirstRun_CreatesWorkspaceAndSeeds()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\my-repo-{tag}";
        var handler = CreateInitHandler();

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path, "My Repo"), default);

        // Assert
        result.Created.Should().BeTrue();
        result.LanesAdded.Should().Equal("Backlog", "To Do", "Doing", "Done");
        result.Workspace.Name.Should().Be("My Repo");
        result.Workspace.Path.Should().Be(Path.GetFullPath(path));
        var lanes = await _db.Lanes
            .Where(l => l.WorkspaceId == result.Workspace.Id)
            .OrderBy(l => l.Position)
            .ToListAsync();
        lanes.Select(l => l.Name).Should().Equal("Backlog", "To Do", "Doing", "Done");
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task InitWorkspace_MarksSeededLanesAsSystem()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\sys-{tag}";
        var handler = CreateInitHandler();

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path, "Sys Repo"), default);

        // Assert
        var lanes = await _db.Lanes
            .Where(l => l.WorkspaceId == result.Workspace.Id)
            .ToListAsync();
        lanes.Should().AllSatisfy(l => l.IsSystem.Should().BeTrue());
    }

    [Fact]
    public async Task InitWorkspace_DefaultsNameToDirectoryName()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var dir = $"my-repo-{tag}";
        var handler = CreateInitHandler();

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand($@"C:\projects\{dir}"), default);

        // Assert
        result.Workspace.Name.Should().Be(dir);
    }

    [Fact]
    public async Task InitWorkspace_Rerun_IsNoOp()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\alpha-{tag}";
        var handler = CreateInitHandler();
        await handler.Handle(new InitWorkspaceCommand(path, "Alpha"), default);

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path, "Alpha"), default);

        // Assert
        result.Created.Should().BeFalse();
        result.LanesAdded.Should().BeEmpty();
        var workspaceCount = await _db.Workspaces.CountAsync(w => w.Path == result.Workspace.Path);
        workspaceCount.Should().Be(1);
        var laneCount = await _db.Lanes.CountAsync(l => l.WorkspaceId == result.Workspace.Id);
        laneCount.Should().Be(4);
    }

    [Fact]
    public async Task InitWorkspace_PartiallySeeded_FillsGap()
    {
        // Arrange: create workspace with only "To Do" and "Done" (remove "Doing")
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\partial-{tag}";
        var ws = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand($"Partial-{tag}", path), default);
        var doingLane = await _db.Lanes.FirstAsync(l => l.WorkspaceId == ws.Id && l.Name == "Doing");
        _db.Lanes.Remove(doingLane);
        await _db.SaveChangesAsync();
        var handler = CreateInitHandler();

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        // Assert
        result.Created.Should().BeFalse();
        result.LanesAdded.Should().Equal("Doing");
        var laneCount = await _db.Lanes.CountAsync(l => l.WorkspaceId == ws.Id);
        laneCount.Should().Be(4);
    }

    [Fact]
    public async Task InitWorkspace_BacklogMissing_InsertsAtPositionOneAndShiftsOthers()
    {
        // Arrange: simulate pre-migration state — workspace has To Do/Doing/Done at positions 1/2/3
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\no-backlog-{tag}";
        var ws = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand($"NoBacklog-{tag}", path), default);
        var backlogLane = await _db.Lanes.FirstAsync(l => l.WorkspaceId == ws.Id && l.Name == "Backlog");
        _db.Lanes.Remove(backlogLane);
        var remaining = await _db.Lanes.Where(l => l.WorkspaceId == ws.Id).OrderBy(l => l.Position).ToListAsync();
        for (var i = 0; i < remaining.Count; i++) remaining[i].Position = i + 1;
        await _db.SaveChangesAsync();
        var handler = CreateInitHandler();

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        // Assert
        result.LanesAdded.Should().Equal("Backlog");
        var lanes = await _db.Lanes
            .Where(l => l.WorkspaceId == ws.Id)
            .OrderBy(l => l.Position)
            .ToListAsync();
        lanes.Should().HaveCount(4);
        lanes.Select(l => l.Name).Should().Equal("Backlog", "To Do", "Doing", "Done");
        lanes[0].IsSystem.Should().BeTrue();
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task InitWorkspace_UserCreatedBacklog_PromotesToSystemLane()
    {
        // Arrange: workspace with a user-created Backlog lane (IsSystem = false)
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\user-backlog-{tag}";
        var ws = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand($"UserBacklog-{tag}", path), default);
        var backlogLane = await _db.Lanes.FirstAsync(l => l.WorkspaceId == ws.Id && l.Name == "Backlog");
        backlogLane.IsSystem = false;
        await _db.SaveChangesAsync();
        var handler = CreateInitHandler();

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        // Assert — nothing added (lane already exists), but IsSystem was promoted
        result.LanesAdded.Should().BeEmpty();
        var promoted = await _db.Lanes.AsNoTracking()
            .FirstAsync(l => l.WorkspaceId == ws.Id && l.Name == "Backlog");
        promoted.IsSystem.Should().BeTrue();
        // Position must be unchanged — it was already at position 1
        promoted.Position.Should().Be(backlogLane.Position);
    }

    [Fact]
    public async Task InitWorkspace_BacklogReconciliation_IsIdempotent()
    {
        // Arrange: fresh workspace (has all 4 system lanes including Backlog)
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\idem-backlog-{tag}";
        var handler = CreateInitHandler();
        await handler.Handle(new InitWorkspaceCommand(path, "Idem Backlog"), default);

        // Act — run reconciliation a second time
        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        // Assert — no changes
        result.LanesAdded.Should().BeEmpty();
        var lanes = await _db.Lanes
            .Where(l => l.WorkspaceId == result.Workspace.Id)
            .OrderBy(l => l.Position)
            .ToListAsync();
        lanes.Should().HaveCount(4);
        lanes.Select(l => l.Name).Should().Equal("Backlog", "To Do", "Doing", "Done");
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task InitWorkspace_PathMatchIsCaseInsensitive()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var handler = CreateInitHandler();
        await handler.Handle(new InitWorkspaceCommand($@"C:\Projects\Repo-{tag}", "Repo"), default);

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand($@"C:\projects\repo-{tag}"), default);

        // Assert
        result.Created.Should().BeFalse();
        result.LanesAdded.Should().BeEmpty();
    }

    [Fact]
    public async Task InitWorkspace_FirstRun_SeedsCanonicalTags()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\tagged-{tag}";
        var handler = CreateInitHandler();

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path, "Tagged Repo"), default);

        // Assert
        result.TagsAdded.Should().BeEquivalentTo(
            ["feature", "bug", "chore", "docs", "arch", "test", "spike"],
            opts => opts.WithoutStrictOrdering());
        var tags = await _db.Tags
            .Where(t => t.WorkspaceId == result.Workspace.Id)
            .Select(t => t.Name)
            .ToListAsync();
        tags.Should().BeEquivalentTo(
            ["feature", "bug", "chore", "docs", "arch", "test", "spike"],
            opts => opts.WithoutStrictOrdering());
    }

    [Fact]
    public async Task InitWorkspace_FirstRun_SeedsCanonicalTagColours()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\colour-{tag}";
        var handler = CreateInitHandler();

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path, "Coloured"), default);

        // Assert
        var seeded = await _db.Tags
            .Where(t => t.WorkspaceId == result.Workspace.Id)
            .ToDictionaryAsync(t => t.Name, t => t.Colour, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, expected) in BrandTagPalette.DefaultColours)
            seeded[name].Should().Be(expected);
    }

    [Fact]
    public async Task InitWorkspace_NoTags_SkipsTagSeeding()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\notags-{tag}";
        var handler = CreateInitHandler();

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path, SeedTags: false), default);

        // Assert
        result.TagsAdded.Should().BeEmpty();
        var tagCount = await _db.Tags.CountAsync(t => t.WorkspaceId == result.Workspace.Id);
        tagCount.Should().Be(0);
    }

    [Fact]
    public async Task InitWorkspace_Rerun_DoesNotDuplicateTags()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\idem-{tag}";
        var handler = CreateInitHandler();
        await handler.Handle(new InitWorkspaceCommand(path, "Idem"), default);

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        // Assert
        result.TagsAdded.Should().BeEmpty();
        var tagCount = await _db.Tags.CountAsync(t => t.WorkspaceId == result.Workspace.Id);
        tagCount.Should().Be(7);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo.git", "owner/repo")]
    [InlineData("https://github.com/owner/repo", "owner/repo")]
    [InlineData("http://github.com/owner/repo.git", "owner/repo")]
    [InlineData("git@github.com:owner/repo.git", "owner/repo")]
    [InlineData("git@github.com:owner/repo", "owner/repo")]
    public async Task InitWorkspace_GitHubOrigin_LinksRepoAndReturnsTrue(string originUrl, string expectedSlug)
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\gh-{tag}";
        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(originUrl);
        var handler = CreateInitHandler(git);

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        // Assert
        result.GitHubLinked.Should().BeTrue();
        result.Workspace.GitHubRepo.Should().Be(expectedSlug);
    }

    [Fact]
    public async Task InitWorkspace_NonGitHubOrigin_DoesNotLink()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\ngh-{tag}";
        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://dev.azure.com/org/project/_git/repo");
        var handler = CreateInitHandler(git);

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        // Assert
        result.GitHubLinked.Should().BeFalse();
        result.Workspace.GitHubRepo.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task InitWorkspace_NoGitHubDetect_SkipsDetection()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\skip-{tag}";
        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://github.com/owner/repo.git");
        var handler = CreateInitHandler(git);

        // Act
        var result = await handler.Handle(
            new InitWorkspaceCommand(path, DetectGitHub: false), default);

        // Assert
        result.GitHubLinked.Should().BeFalse();
        result.Workspace.GitHubRepo.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task InitWorkspace_GitHubAlreadyLinked_DoesNotOverwrite()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\keep-{tag}";
        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://github.com/new-owner/new-repo.git");
        var handler = CreateInitHandler(git);
        var first = await handler.Handle(new InitWorkspaceCommand(path), default);
        first.GitHubLinked.Should().BeTrue();

        // Act — rerun with a different origin; existing link must be preserved
        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        // Assert
        result.GitHubLinked.Should().BeFalse();
        result.Workspace.GitHubRepo.Should().Be("new-owner/new-repo");
    }

    [Fact]
    public async Task InitWorkspace_NullPath_Throws()
    {
        var handler = CreateInitHandler();

        var act = () => handler.Handle(new InitWorkspaceCommand(null!), default);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InitWorkspace_EmptyPath_Throws()
    {
        var handler = CreateInitHandler();

        var act = () => handler.Handle(new InitWorkspaceCommand(""), default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InitWorkspace_NonExistentDirectory_CreatesWorkspaceWithDirectoryName()
    {
        var dirName = $"ghost-{Guid.NewGuid():N}"[..20];
        var path = $@"C:\does-not-exist\{dirName}";
        var handler = CreateInitHandler();

        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        result.Created.Should().BeTrue();
        result.Workspace.Name.Should().Be(dirName);
    }

    [Theory]
    [InlineData("https://github.com/")]
    [InlineData("https://github.com/owner")]
    [InlineData("/owner/repo")]
    [InlineData("https://gitlab.com/owner/repo")]
    public async Task InitWorkspace_InvalidOrNonGitHubOriginVariants_DoNotLink(string originUrl)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\slug-{tag}";
        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(originUrl);
        var handler = CreateInitHandler(git);

        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        result.GitHubLinked.Should().BeFalse();
        result.Workspace.GitHubRepo.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task InitWorkspace_PartiallySeeded_TagsMissing_FillsGapTags()
    {
        // Arrange: CreateWorkspaceCommandHandler seeds lanes but not tags
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\taggap-{tag}";
        var ws = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand($"TagGap-{tag}", path), default);
        (await _db.Tags.CountAsync(t => t.WorkspaceId == ws.Id)).Should().Be(0);
        var handler = CreateInitHandler();

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        // Assert
        result.Created.Should().BeFalse();
        result.LanesAdded.Should().BeEmpty();
        result.TagsAdded.Should().BeEquivalentTo(
            ["feature", "bug", "chore", "docs", "arch", "test", "spike"],
            opts => opts.WithoutStrictOrdering());
        (await _db.Tags.CountAsync(t => t.WorkspaceId == ws.Id)).Should().Be(7);
    }

    [Fact]
    public async Task InitWorkspace_SaveChangesThrows_PropagatesException()
    {
        // Arrange: fresh SQLite with schema; contexts from this factory throw on every save.
        var dbName = Guid.NewGuid().ToString("N");
        var connStr = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        await using var conn = new SqliteConnection(connStr);
        conn.Open();
        var baseOpts = new DbContextOptionsBuilder<BishopDbContext>().UseSqlite(connStr).Options;
        await using (var schemaCtx = new BishopDbContext(baseOpts))
            schemaCtx.Database.EnsureCreated();

        var throwOpts = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(connStr)
            .AddInterceptors(new ThrowingSaveChangesInterceptor())
            .Options;
        var handler = new InitWorkspaceCommandHandler(
            new DirectDbContextFactory(throwOpts), Substitute.For<IGitCli>());

        // Act
        var act = () => handler.Handle(new InitWorkspaceCommand(@"C:\new-ws-save-fail"), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated DB failure");
    }

    [Fact]
    public async Task InitWorkspace_ExistingWorkspace_GetOriginUrlThrows_PropagatesException()
    {
        // Arrange: first run (no detection) creates the workspace.
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\ex-exist-{tag}";
        await CreateInitHandler().Handle(new InitWorkspaceCommand(path, DetectGitHub: false), default);

        // Second run: git throws inside DetectGitHubAsync.
        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string?>(new InvalidOperationException("git failed on rerun")));

        // Act
        var act = () => CreateInitHandler(git).Handle(new InitWorkspaceCommand(path), default);

        // Assert — exception propagates end-to-end through DetectGitHubAsync.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("git failed on rerun");
    }

    [Fact]
    public async Task InitWorkspace_GitOriginReturnsNull_SkipsGitHubDetection()
    {
        // CreateInitHandler() stubs GetOriginUrlAsync to return null by default.
        // No exception is raised and the result contains no GitHub link.
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\null-git-{tag}";
        var handler = CreateInitHandler();

        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        result.GitHubLinked.Should().BeFalse();
        result.Workspace.GitHubRepo.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SetWorkspaceGitHubRepo_PlainOwnerRepo_PersistsAndReturnsWorkspace()
    {
        // Arrange
        var name = U("Repo");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_factory);

        // Act
        var result = await handler.Handle(
            new SetWorkspaceGitHubRepoCommand(workspace.Id, "owner/repo"), default);

        // Assert
        result.GitHubRepo.Should().Be("owner/repo");
        result.UpdatedAt.Should().BeOnOrAfter(result.CreatedAt);
        (await _db.Workspaces.FindAsync(workspace.Id))!.GitHubRepo.Should().Be("owner/repo");
    }

    [Theory]
    [InlineData("https://github.com/owner/repo", "owner/repo")]
    [InlineData("https://github.com/owner/repo.git", "owner/repo")]
    [InlineData("https://github.com/owner/repo/", "owner/repo")]
    [InlineData("http://github.com/owner/repo", "owner/repo")]
    [InlineData("git@github.com:owner/repo.git", "owner/repo")]
    [InlineData("git@github.com:owner/repo", "owner/repo")]
    [InlineData("  owner/repo  ", "owner/repo")]
    public async Task SetWorkspaceGitHubRepo_NormalizesVariousFormats(string input, string expected)
    {
        // Arrange
        var name = U("Repo");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_factory);

        // Act
        var result = await handler.Handle(
            new SetWorkspaceGitHubRepoCommand(workspace.Id, input), default);

        // Assert
        result.GitHubRepo.Should().Be(expected);
    }

    [Theory]
    [InlineData("justarepo")]
    [InlineData("owner/repo/extra")]
    [InlineData("/owner/repo")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("repo/")]
    public async Task SetWorkspaceGitHubRepo_InvalidFormat_Throws(string? input)
    {
        // Arrange
        var name = U("Repo");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_factory);

        // Act
        var act = () => handler.Handle(new SetWorkspaceGitHubRepoCommand(workspace.Id, input!), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*owner/repo*");
    }

    [Fact]
    public async Task SetWorkspaceGitHubRepo_WorkspaceNotFound_Throws()
    {
        // Arrange
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_factory);

        // Act
        var act = () => handler.Handle(
            new SetWorkspaceGitHubRepoCommand(Guid.NewGuid(), "owner/repo"), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ── UnsetWorkspaceGitHubRepoCommandHandler ───────────────────────────────

    [Fact]
    public async Task UnsetWorkspaceGitHubRepo_ClearsGitHubRepoAndPersists()
    {
        // Arrange
        var name = U("Repo");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        await new SetWorkspaceGitHubRepoCommandHandler(_factory)
            .Handle(new SetWorkspaceGitHubRepoCommand(workspace.Id, "owner/repo"), default);
        var handler = new UnsetWorkspaceGitHubRepoCommandHandler(_factory);

        // Act
        var result = await handler.Handle(new UnsetWorkspaceGitHubRepoCommand(workspace.Id), default);

        // Assert
        result.GitHubRepo.Should().BeNull();
        (await _db.Workspaces.FindAsync(workspace.Id))!.GitHubRepo.Should().BeNull();
    }

    [Fact]
    public async Task UnsetWorkspaceGitHubRepo_WorkspaceNotFound_Throws()
    {
        // Arrange
        var handler = new UnsetWorkspaceGitHubRepoCommandHandler(_factory);

        // Act
        var act = () => handler.Handle(new UnsetWorkspaceGitHubRepoCommand(Guid.NewGuid()), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ── ReorderWorkspacesCommandHandler ──────────────────────────────────────

    [Fact]
    public async Task ReorderWorkspaces_UpdatesPositionsInOrder()
    {
        // Arrange
        var create = new CreateWorkspaceCommandHandler(_factory);
        var ws1 = await create.Handle(new CreateWorkspaceCommand(U("First"), $@"C:\{U()}"), default);
        var ws2 = await create.Handle(new CreateWorkspaceCommand(U("Second"), $@"C:\{U()}"), default);
        var handler = new ReorderWorkspacesCommandHandler(_factory);

        // Act
        await handler.Handle(new ReorderWorkspacesCommand([ws2.Id, ws1.Id]), default);

        // Assert
        var updated1 = (await _db.Workspaces.FindAsync(ws1.Id))!;
        var updated2 = (await _db.Workspaces.FindAsync(ws2.Id))!;
        updated2.Position.Should().Be(1);
        updated1.Position.Should().Be(2);
    }

    [Fact]
    public async Task ReorderWorkspaces_WorkspaceNotInList_KeepsItsPosition()
    {
        // Arrange
        var create = new CreateWorkspaceCommandHandler(_factory);
        var ws1 = await create.Handle(new CreateWorkspaceCommand(U("Keep"), $@"C:\{U()}"), default);
        var ws2 = await create.Handle(new CreateWorkspaceCommand(U("Move"), $@"C:\{U()}"), default);
        var originalWs1Position = ws1.Position;
        var handler = new ReorderWorkspacesCommandHandler(_factory);

        // Act — only reorder ws2; ws1 is absent from the list
        await handler.Handle(new ReorderWorkspacesCommand([ws2.Id]), default);

        // Assert
        var updated1 = (await _db.Workspaces.FindAsync(ws1.Id))!;
        updated1.Position.Should().Be(originalWs1Position);
    }

    // ── LaunchPlainTerminalCommandHandler ────────────────────────────────────

    [Fact]
    public async Task LaunchPlainTerminal_ReturnsTrue_WhenLauncherSucceeds()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchPlain(Arg.Any<string>(), Arg.Any<TerminalSnap?>()).Returns(true);
        var handler = new LaunchPlainTerminalCommandHandler(launcher);

        // Act
        var result = await handler.Handle(new LaunchPlainTerminalCommand(@"C:\workspace"), default);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task LaunchPlainTerminal_ReturnsFalse_WhenLauncherReturnsFalse()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchPlain(Arg.Any<string>(), Arg.Any<TerminalSnap?>()).Returns(false);
        var handler = new LaunchPlainTerminalCommandHandler(launcher);

        // Act
        var result = await handler.Handle(new LaunchPlainTerminalCommand(@"C:\workspace"), default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task LaunchPlainTerminal_ForwardsPathAndSnapToLauncher()
    {
        // Arrange
        var snap = new TerminalSnap(0, 0, 800, 600);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchPlainTerminalCommandHandler(launcher);

        // Act
        await handler.Handle(new LaunchPlainTerminalCommand(@"C:\workspace", snap), default);

        // Assert
        launcher.Received(1).LaunchPlain(@"C:\workspace", snap);
    }

    // ── LaunchWorkspaceCommandHandler ────────────────────────────────────────

    [Fact]
    public async Task LaunchWorkspace_ReturnsTrue_WhenLauncherSucceeds()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(true);
        var handler = new LaunchWorkspaceCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>(), Substitute.For<IDefaultTagSeeder>(), Substitute.For<ISystemLaneSeeder>());

        // Act
        var result = await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace"), default);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task LaunchWorkspace_ReturnsFalse_WhenLauncherReturnsFalse()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(false);
        var handler = new LaunchWorkspaceCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>(), Substitute.For<IDefaultTagSeeder>(), Substitute.For<ISystemLaneSeeder>());

        // Act
        var result = await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace"), default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task LaunchWorkspace_PassesNullClaudeArgsAndForwardsSnap()
    {
        // Arrange
        var snap = new TerminalSnap(0, 0, 800, 600);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkspaceCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>(), Substitute.For<IDefaultTagSeeder>(), Substitute.For<ISystemLaneSeeder>());

        // Act
        await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace", snap), default);

        // Assert
        launcher.Received(1).Launch(@"C:\workspace", null, snap);
    }

    // ── SystemLaneSeeder ─────────────────────────────────────────────────────

    [Fact]
    public async Task SystemLaneSeeder_UserCreatedBacklog_PromotesToSystemLane()
    {
        // Arrange: workspace with a user-created Backlog lane (IsSystem = false)
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\seeder-{tag}";
        var ws = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand($"Seeder-{tag}", path), default);
        var backlogLane = await _db.Lanes.FirstAsync(l => l.WorkspaceId == ws.Id && l.Name == "Backlog");
        backlogLane.IsSystem = false;
        await _db.SaveChangesAsync();
        var seeder = new SystemLaneSeeder(_factory);

        // Act
        await seeder.EnsureAsync(path);

        // Assert
        var promoted = await _db.Lanes.AsNoTracking()
            .FirstAsync(l => l.WorkspaceId == ws.Id && l.Name == "Backlog");
        promoted.IsSystem.Should().BeTrue();
        promoted.Position.Should().Be(backlogLane.Position);
    }

    [Fact]
    public async Task SystemLaneSeeder_BacklogMissing_InsertsAtPositionOne()
    {
        // Arrange: workspace without Backlog lane, remaining lanes at positions 1/2/3
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\seeder-nob-{tag}";
        var ws = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand($"SeederNoB-{tag}", path), default);
        var backlogLane = await _db.Lanes.FirstAsync(l => l.WorkspaceId == ws.Id && l.Name == "Backlog");
        var remaining = await _db.Lanes
            .Where(l => l.WorkspaceId == ws.Id && l.Id != backlogLane.Id)
            .OrderBy(l => l.Position)
            .ToListAsync();
        for (var i = 0; i < remaining.Count; i++) remaining[i].Position = i + 1;
        _db.Lanes.Remove(backlogLane);
        await _db.SaveChangesAsync();
        var seeder = new SystemLaneSeeder(_factory);

        // Act
        await seeder.EnsureAsync(path);

        // Assert
        var lanes = await _db.Lanes.AsNoTracking()
            .Where(l => l.WorkspaceId == ws.Id)
            .OrderBy(l => l.Position)
            .ToListAsync();
        lanes.Should().HaveCount(4);
        lanes.Select(l => l.Name).Should().Equal("Backlog", "To Do", "Doing", "Done");
        lanes[0].IsSystem.Should().BeTrue();
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task SystemLaneSeeder_AlreadyCorrect_IsNoOp()
    {
        // Arrange: fully seeded workspace
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\seeder-idem-{tag}";
        await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand($"SeederIdem-{tag}", path), default);
        var seeder = new SystemLaneSeeder(_factory);

        // Act — run twice
        await seeder.EnsureAsync(path);
        await seeder.EnsureAsync(path);

        // Assert — lane count unchanged
        var ws = await _db.Workspaces.FirstAsync(w => w.Path == Path.GetFullPath(path));
        var laneCount = await _db.Lanes.CountAsync(l => l.WorkspaceId == ws.Id);
        laneCount.Should().Be(4);
    }

    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated DB failure");
    }

    private sealed class DirectDbContextFactory : IDbContextFactory<BishopDbContext>
    {
        private readonly DbContextOptions<BishopDbContext> _options;
        public DirectDbContextFactory(DbContextOptions<BishopDbContext> options) => _options = options;
        public BishopDbContext CreateDbContext() => new(_options);
    }
}
