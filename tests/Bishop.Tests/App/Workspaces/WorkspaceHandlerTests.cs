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
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Workspaces;

public sealed class WorkspaceHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;

    public WorkspaceHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    [Fact]
    public async Task CreateWorkspace_PersistsAndReturnsWorkspace()
    {
        // Arrange
        var name = U("MyRepo");
        var handler = new CreateWorkspaceCommandHandler(_db);

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
        var create = new CreateWorkspaceCommandHandler(_db);
        var beta = await create.Handle(new CreateWorkspaceCommand(betaName, $@"C:\{betaName}"), default);
        var alpha = await create.Handle(new CreateWorkspaceCommand(alphaName, $@"C:\{alphaName}"), default);
        var handler = new ListWorkspacesQueryHandler(_db);

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
        var created = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new GetWorkspaceQueryHandler(_db);

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
        var handler = new GetWorkspaceQueryHandler(_db);

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
        var created = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var handler = new GetWorkspaceQueryHandler(_db);

        // Act
        var result = await handler.Handle(new GetWorkspaceQuery(created.Id), default);

        // Assert
        result.Should().NotBeNull();
        result!.Lanes.Should().HaveCount(3);
        result.Lanes.Select(l => l.Position).Should().BeInAscendingOrder();
        result.Lanes.Select(l => l.Name).Should().Equal("To Do", "Doing", "Done");
    }

    [Fact]
    public async Task UpdateWorkspace_ChangesNameAndPath()
    {
        // Arrange
        var oldName = U("Old");
        var newName = U("New");
        var created = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(oldName, $@"C:\{oldName}"), default);
        var handler = new UpdateWorkspaceCommandHandler(_db);

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
        var created = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);

        // Act
        await new DeleteWorkspaceCommandHandler(_db)
            .Handle(new DeleteWorkspaceCommand(created.Id), default);

        // Assert
        var remaining = await new ListWorkspacesQueryHandler(_db)
            .Handle(new ListWorkspacesQuery(), default);
        remaining.Should().NotContain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task InitWorkspace_FirstRun_CreatesWorkspaceAndSeeds()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\my-repo-{tag}";
        var handler = new InitWorkspaceCommandHandler(_db);

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path, "My Repo"), default);

        // Assert
        result.Created.Should().BeTrue();
        result.LanesAdded.Should().Equal("To Do", "Doing", "Done");
        result.Workspace.Name.Should().Be("My Repo");
        result.Workspace.Path.Should().Be(Path.GetFullPath(path));
        var lanes = await _db.Lanes
            .Where(l => l.WorkspaceId == result.Workspace.Id)
            .OrderBy(l => l.Position)
            .ToListAsync();
        lanes.Select(l => l.Name).Should().Equal("To Do", "Doing", "Done");
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task InitWorkspace_DefaultsNameToDirectoryName()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var dir = $"my-repo-{tag}";
        var handler = new InitWorkspaceCommandHandler(_db);

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
        var handler = new InitWorkspaceCommandHandler(_db);
        await handler.Handle(new InitWorkspaceCommand(path, "Alpha"), default);

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path, "Alpha"), default);

        // Assert
        result.Created.Should().BeFalse();
        result.LanesAdded.Should().BeEmpty();
        var workspaceCount = await _db.Workspaces.CountAsync(w => w.Path == result.Workspace.Path);
        workspaceCount.Should().Be(1);
        var laneCount = await _db.Lanes.CountAsync(l => l.WorkspaceId == result.Workspace.Id);
        laneCount.Should().Be(3);
    }

    [Fact]
    public async Task InitWorkspace_PartiallySeeded_FillsGap()
    {
        // Arrange: create workspace with only "To Do" and "Done" (remove "Doing")
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\partial-{tag}";
        var ws = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand($"Partial-{tag}", path), default);
        var doingLane = await _db.Lanes.FirstAsync(l => l.WorkspaceId == ws.Id && l.Name == "Doing");
        _db.Lanes.Remove(doingLane);
        await _db.SaveChangesAsync();
        var handler = new InitWorkspaceCommandHandler(_db);

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        // Assert
        result.Created.Should().BeFalse();
        result.LanesAdded.Should().Equal("Doing");
        var laneCount = await _db.Lanes.CountAsync(l => l.WorkspaceId == ws.Id);
        laneCount.Should().Be(3);
    }

    [Fact]
    public async Task InitWorkspace_PathMatchIsCaseInsensitive()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N")[..8];
        var handler = new InitWorkspaceCommandHandler(_db);
        await handler.Handle(new InitWorkspaceCommand($@"C:\Projects\Repo-{tag}", "Repo"), default);

        // Act
        var result = await handler.Handle(new InitWorkspaceCommand($@"C:\projects\repo-{tag}"), default);

        // Assert
        result.Created.Should().BeFalse();
        result.LanesAdded.Should().BeEmpty();
    }

    [Fact]
    public async Task SetWorkspaceGitHubRepo_PlainOwnerRepo_PersistsAndReturnsWorkspace()
    {
        // Arrange
        var name = U("Repo");
        var workspace = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_db);

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
        var workspace = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_db);

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
    public async Task SetWorkspaceGitHubRepo_InvalidFormat_Throws(string input)
    {
        // Arrange
        var name = U("Repo");
        var workspace = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_db);

        // Act
        var act = () => handler.Handle(new SetWorkspaceGitHubRepoCommand(workspace.Id, input), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*owner/repo*");
    }

    [Fact]
    public async Task SetWorkspaceGitHubRepo_WorkspaceNotFound_Throws()
    {
        // Arrange
        var handler = new SetWorkspaceGitHubRepoCommandHandler(_db);

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
        var workspace = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        await new SetWorkspaceGitHubRepoCommandHandler(_db)
            .Handle(new SetWorkspaceGitHubRepoCommand(workspace.Id, "owner/repo"), default);
        var handler = new UnsetWorkspaceGitHubRepoCommandHandler(_db);

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
        var handler = new UnsetWorkspaceGitHubRepoCommandHandler(_db);

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
        var create = new CreateWorkspaceCommandHandler(_db);
        var ws1 = await create.Handle(new CreateWorkspaceCommand(U("First"), $@"C:\{U()}"), default);
        var ws2 = await create.Handle(new CreateWorkspaceCommand(U("Second"), $@"C:\{U()}"), default);
        var handler = new ReorderWorkspacesCommandHandler(_db);

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
        var create = new CreateWorkspaceCommandHandler(_db);
        var ws1 = await create.Handle(new CreateWorkspaceCommand(U("Keep"), $@"C:\{U()}"), default);
        var ws2 = await create.Handle(new CreateWorkspaceCommand(U("Move"), $@"C:\{U()}"), default);
        var originalWs1Position = ws1.Position;
        var handler = new ReorderWorkspacesCommandHandler(_db);

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
        var handler = new LaunchWorkspaceCommandHandler(launcher);

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
        var handler = new LaunchWorkspaceCommandHandler(launcher);

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
        var handler = new LaunchWorkspaceCommandHandler(launcher);

        // Act
        await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace", snap), default);

        // Assert
        launcher.Received(1).Launch(@"C:\workspace", null, snap);
    }
}
