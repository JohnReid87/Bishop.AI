using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.App.Workspaces.DeleteWorkspace;
using Bishop.App.Workspaces.GetWorkspace;
using Bishop.App.Workspaces.InitWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.SetWorkspaceGitHubRepo;
using Bishop.App.Workspaces.UpdateWorkspace;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

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
}
