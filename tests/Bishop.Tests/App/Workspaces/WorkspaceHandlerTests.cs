using Bishop.App.Git;
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
using Bishop.App.Workspaces.RemoveWorkspace;
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
        var name = U("MyRepo");
        var handler = new CreateWorkspaceCommandHandler(_factory);

        var result = await handler.Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be(name);
        result.Path.Should().Be($@"C:\code\{name}");
        result.CreatedAt.Should().NotBe(default);
        result.UpdatedAt.Should().Be(result.CreatedAt);
    }

    [Fact]
    public async Task ListWorkspaces_ReturnsOrderedByPosition()
    {
        var betaName = U("Beta");
        var alphaName = U("Alpha");
        var create = new CreateWorkspaceCommandHandler(_factory);
        var beta = await create.Handle(new CreateWorkspaceCommand(betaName, $@"C:\{betaName}"), default);
        var alpha = await create.Handle(new CreateWorkspaceCommand(alphaName, $@"C:\{alphaName}"), default);
        var handler = new ListWorkspacesQueryHandler(_factory);

        var result = await handler.Handle(new ListWorkspacesQuery(), default);

        var ours = result.Where(w => w.Id == beta.Id || w.Id == alpha.Id).ToList();
        ours.Should().HaveCount(2);
        ours[0].Name.Should().Be(betaName);
        ours[1].Name.Should().Be(alphaName);
    }

    [Fact]
    public async Task GetWorkspace_ReturnsCorrectWorkspace()
    {
        var name = U("MyRepo");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new GetWorkspaceQueryHandler(_factory);

        var result = await handler.Handle(new GetWorkspaceQuery(created.Id), default);

        result.Should().NotBeNull();
        result!.Name.Should().Be(name);
    }

    [Fact]
    public async Task GetWorkspace_ReturnsNullForUnknownId()
    {
        var handler = new GetWorkspaceQueryHandler(_factory);

        var result = await handler.Handle(new GetWorkspaceQuery(Guid.NewGuid()), default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateWorkspace_ChangesNameAndPath()
    {
        var oldName = U("Old");
        var newName = U("New");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(oldName, $@"C:\{oldName}"), default);
        var handler = new UpdateWorkspaceCommandHandler(_factory);

        var result = await handler.Handle(
            new UpdateWorkspaceCommand(created.Id, newName, $@"C:\{newName}"), default);

        result.Name.Should().Be(newName);
        result.Path.Should().Be($@"C:\{newName}");
        result.UpdatedAt.Should().BeOnOrAfter(result.CreatedAt);
    }

    [Fact]
    public async Task DeleteWorkspace_RemovesFromDatabase()
    {
        var name = U("ToDelete");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);

        await new DeleteWorkspaceCommandHandler(_factory)
            .Handle(new DeleteWorkspaceCommand(created.Id), default);

        var remaining = await new ListWorkspacesQueryHandler(_factory)
            .Handle(new ListWorkspacesQuery(), default);
        remaining.Should().NotContain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task InitWorkspace_FirstRun_CreatesWorkspace()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\my-repo-{tag}";
        var handler = CreateInitHandler();

        var result = await handler.Handle(new InitWorkspaceCommand(path, "My Repo"), default);

        result.Created.Should().BeTrue();
        result.Workspace.Name.Should().Be("My Repo");
        result.Workspace.Path.Should().Be(Path.GetFullPath(path));
    }

    [Fact]
    public async Task InitWorkspace_DefaultsNameToDirectoryName()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var dir = $"my-repo-{tag}";
        var handler = CreateInitHandler();

        var result = await handler.Handle(new InitWorkspaceCommand($@"C:\projects\{dir}"), default);

        result.Workspace.Name.Should().Be(dir);
    }

    [Fact]
    public async Task InitWorkspace_Rerun_IsNoOp()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\alpha-{tag}";
        var handler = CreateInitHandler();
        await handler.Handle(new InitWorkspaceCommand(path, "Alpha"), default);

        var result = await handler.Handle(new InitWorkspaceCommand(path, "Alpha"), default);

        result.Created.Should().BeFalse();
        var workspaceCount = await _db.Workspaces.CountAsync(w => w.Path == result.Workspace.Path);
        workspaceCount.Should().Be(1);
    }

    [Fact]
    public async Task InitWorkspace_PathMatchIsCaseInsensitive()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var handler = CreateInitHandler();
        await handler.Handle(new InitWorkspaceCommand($@"C:\Projects\Repo-{tag}", "Repo"), default);

        var result = await handler.Handle(new InitWorkspaceCommand($@"C:\projects\repo-{tag}"), default);

        result.Created.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://github.com/owner/repo.git", "owner/repo")]
    [InlineData("https://github.com/owner/repo", "owner/repo")]
    [InlineData("http://github.com/owner/repo.git", "owner/repo")]
    [InlineData("git@github.com:owner/repo.git", "owner/repo")]
    [InlineData("git@github.com:owner/repo", "owner/repo")]
    public async Task InitWorkspace_GitHubOrigin_LinksRepoAndReturnsTrue(string originUrl, string expectedSlug)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\gh-{tag}";
        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(originUrl);
        var handler = CreateInitHandler(git);

        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        result.GitHubLinked.Should().BeTrue();
        result.Workspace.GitHubRepo.Should().Be(expectedSlug);
    }

    [Fact]
    public async Task InitWorkspace_NonGitHubOrigin_DoesNotLink()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\ngh-{tag}";
        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://dev.azure.com/org/project/_git/repo");
        var handler = CreateInitHandler(git);

        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        result.GitHubLinked.Should().BeFalse();
        result.Workspace.GitHubRepo.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task InitWorkspace_NoGitHubDetect_SkipsDetection()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\skip-{tag}";
        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://github.com/owner/repo.git");
        var handler = CreateInitHandler(git);

        var result = await handler.Handle(
            new InitWorkspaceCommand(path, DetectGitHub: false), default);

        result.GitHubLinked.Should().BeFalse();
        result.Workspace.GitHubRepo.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task InitWorkspace_GitHubAlreadyLinked_DoesNotOverwrite()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\keep-{tag}";
        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://github.com/new-owner/new-repo.git");
        var handler = CreateInitHandler(git);
        var first = await handler.Handle(new InitWorkspaceCommand(path), default);
        first.GitHubLinked.Should().BeTrue();

        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

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
    public async Task InitWorkspace_SaveChangesThrows_PropagatesException()
    {
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

        var act = () => handler.Handle(new InitWorkspaceCommand(@"C:\new-ws-save-fail"), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated DB failure");
    }

    [Fact]
    public async Task InitWorkspace_ExistingWorkspace_GetOriginUrlThrows_PropagatesException()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\ex-exist-{tag}";
        await CreateInitHandler().Handle(new InitWorkspaceCommand(path, DetectGitHub: false), default);

        var git = Substitute.For<IGitCli>();
        git.GetOriginUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string?>(new InvalidOperationException("git failed on rerun")));

        var act = () => CreateInitHandler(git).Handle(new InitWorkspaceCommand(path), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("git failed on rerun");
    }

    [Fact]
    public async Task InitWorkspace_GitOriginReturnsNull_SkipsGitHubDetection()
    {
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
        var name = U("Repo");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_factory);

        var result = await handler.Handle(
            new SetWorkspaceGitHubRepoCommand(workspace.Id, "owner/repo"), default);

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
        var name = U("Repo");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_factory);

        var result = await handler.Handle(
            new SetWorkspaceGitHubRepoCommand(workspace.Id, input), default);

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
        var name = U("Repo");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_factory);

        var act = () => handler.Handle(new SetWorkspaceGitHubRepoCommand(workspace.Id, input!), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*owner/repo*");
    }

    [Fact]
    public async Task SetWorkspaceGitHubRepo_WorkspaceNotFound_Throws()
    {
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_factory);

        var act = () => handler.Handle(
            new SetWorkspaceGitHubRepoCommand(Guid.NewGuid(), "owner/repo"), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UnsetWorkspaceGitHubRepo_ClearsGitHubRepoAndPersists()
    {
        var name = U("Repo");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        await new SetWorkspaceGitHubRepoCommandHandler(_factory)
            .Handle(new SetWorkspaceGitHubRepoCommand(workspace.Id, "owner/repo"), default);
        var handler = new UnsetWorkspaceGitHubRepoCommandHandler(_factory);

        var result = await handler.Handle(new UnsetWorkspaceGitHubRepoCommand(workspace.Id), default);

        result.GitHubRepo.Should().BeNull();
        (await _db.Workspaces.FindAsync(workspace.Id))!.GitHubRepo.Should().BeNull();
    }

    [Fact]
    public async Task UnsetWorkspaceGitHubRepo_WorkspaceNotFound_Throws()
    {
        var handler = new UnsetWorkspaceGitHubRepoCommandHandler(_factory);

        var act = () => handler.Handle(new UnsetWorkspaceGitHubRepoCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ReorderWorkspaces_UpdatesPositionsInOrder()
    {
        var create = new CreateWorkspaceCommandHandler(_factory);
        var ws1 = await create.Handle(new CreateWorkspaceCommand(U("First"), $@"C:\{U()}"), default);
        var ws2 = await create.Handle(new CreateWorkspaceCommand(U("Second"), $@"C:\{U()}"), default);
        var handler = new ReorderWorkspacesCommandHandler(_factory);

        await handler.Handle(new ReorderWorkspacesCommand([ws2.Id, ws1.Id]), default);

        var updated1 = (await _db.Workspaces.FindAsync(ws1.Id))!;
        var updated2 = (await _db.Workspaces.FindAsync(ws2.Id))!;
        updated2.Position.Should().Be(1);
        updated1.Position.Should().Be(2);
    }

    [Fact]
    public async Task ReorderWorkspaces_WorkspaceNotInList_KeepsItsPosition()
    {
        var create = new CreateWorkspaceCommandHandler(_factory);
        var ws1 = await create.Handle(new CreateWorkspaceCommand(U("Keep"), $@"C:\{U()}"), default);
        var ws2 = await create.Handle(new CreateWorkspaceCommand(U("Move"), $@"C:\{U()}"), default);
        var originalWs1Position = ws1.Position;
        var handler = new ReorderWorkspacesCommandHandler(_factory);

        await handler.Handle(new ReorderWorkspacesCommand([ws2.Id]), default);

        var updated1 = (await _db.Workspaces.FindAsync(ws1.Id))!;
        updated1.Position.Should().Be(originalWs1Position);
    }

    [Fact]
    public async Task LaunchPlainTerminal_ReturnsTrue_WhenLauncherSucceeds()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchPlain(Arg.Any<string>(), Arg.Any<TerminalSnap?>()).Returns(true);
        var handler = new LaunchPlainTerminalCommandHandler(launcher);

        var result = await handler.Handle(new LaunchPlainTerminalCommand(@"C:\workspace"), default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task LaunchPlainTerminal_ReturnsFalse_WhenLauncherReturnsFalse()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchPlain(Arg.Any<string>(), Arg.Any<TerminalSnap?>()).Returns(false);
        var handler = new LaunchPlainTerminalCommandHandler(launcher);

        var result = await handler.Handle(new LaunchPlainTerminalCommand(@"C:\workspace"), default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task LaunchPlainTerminal_ForwardsPathAndSnapToLauncher()
    {
        var snap = new TerminalSnap(0, 0, 800, 600);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchPlainTerminalCommandHandler(launcher);

        await handler.Handle(new LaunchPlainTerminalCommand(@"C:\workspace", snap), default);

        launcher.Received(1).LaunchPlain(@"C:\workspace", snap);
    }

    [Fact]
    public async Task LaunchWorkspace_ReturnsTrue_WhenLauncherSucceeds()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(true);
        var handler = new LaunchWorkspaceCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>());

        var result = await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace"), default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task LaunchWorkspace_ReturnsFalse_WhenLauncherReturnsFalse()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(false);
        var handler = new LaunchWorkspaceCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>());

        var result = await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace"), default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task LaunchWorkspace_PassesNullClaudeArgsAndForwardsSnap()
    {
        var snap = new TerminalSnap(0, 0, 800, 600);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkspaceCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>());

        await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace", snap), default);

        launcher.Received(1).Launch(@"C:\workspace", null, snap);
    }

    [Fact]
    public async Task RemoveWorkspace_SetsIsRemovedAndRemovedAt()
    {
        var name = U("ToRemove");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var handler = new RemoveWorkspaceCommandHandler(_factory);

        await handler.Handle(new RemoveWorkspaceCommand(created.Id), default);

        var ws = await _db.Workspaces.FindAsync(created.Id);
        ws!.IsRemoved.Should().BeTrue();
        ws.RemovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveWorkspace_WorkspaceNotFound_Throws()
    {
        var handler = new RemoveWorkspaceCommandHandler(_factory);

        var act = () => handler.Handle(new RemoveWorkspaceCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task RemoveWorkspace_RemovedWorkspaceExcludedFromList()
    {
        var name = U("Removed2");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        await new RemoveWorkspaceCommandHandler(_factory)
            .Handle(new RemoveWorkspaceCommand(created.Id), default);

        var result = await new ListWorkspacesQueryHandler(_factory)
            .Handle(new ListWorkspacesQuery(), default);

        result.Should().NotContain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task ListWorkspaces_ExcludesRemovedWorkspaces()
    {
        var name = U("Removed");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var ws = await _db.Workspaces.FindAsync(created.Id);
        ws!.IsRemoved = true;
        ws.RemovedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var handler = new ListWorkspacesQueryHandler(_factory);

        var result = await handler.Handle(new ListWorkspacesQuery(), default);

        result.Should().NotContain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task ListWorkspaces_IncludesNonRemovedWorkspaces()
    {
        var name = U("Active");
        var created = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var handler = new ListWorkspacesQueryHandler(_factory);

        var result = await handler.Handle(new ListWorkspacesQuery(), default);

        result.Should().Contain(w => w.Id == created.Id);
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
