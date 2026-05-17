using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.App.Workspaces.DeleteWorkspace;
using Bishop.App.Workspaces.GetWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.UpdateWorkspace;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.Workspaces;

public sealed class WorkspaceHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BishopDbContext _db;

    public WorkspaceHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new BishopDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateWorkspace_PersistsAndReturnsWorkspace()
    {
        var handler = new CreateWorkspaceCommandHandler(_db);

        var result = await handler.Handle(new CreateWorkspaceCommand("My Repo", @"C:\code\my-repo"), default);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("My Repo");
        result.Path.Should().Be(@"C:\code\my-repo");
        result.Position.Should().Be(1);
        result.CreatedAt.Should().NotBe(default);
        result.UpdatedAt.Should().Be(result.CreatedAt);
    }

    [Fact]
    public async Task ListWorkspaces_ReturnsOrderedByPosition()
    {
        var create = new CreateWorkspaceCommandHandler(_db);
        await create.Handle(new CreateWorkspaceCommand("Beta", @"C:\beta"), default);
        await create.Handle(new CreateWorkspaceCommand("Alpha", @"C:\alpha"), default);

        var handler = new ListWorkspacesQueryHandler(_db);
        var result = await handler.Handle(new ListWorkspacesQuery(), default);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Beta");
        result[1].Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task GetWorkspace_ReturnsCorrectWorkspace()
    {
        var created = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand("MyRepo", @"C:\code"), default);

        var handler = new GetWorkspaceQueryHandler(_db);
        var result = await handler.Handle(new GetWorkspaceQuery(created.Id), default);

        result.Should().NotBeNull();
        result!.Name.Should().Be("MyRepo");
    }

    [Fact]
    public async Task GetWorkspace_ReturnsNullForUnknownId()
    {
        var handler = new GetWorkspaceQueryHandler(_db);
        var result = await handler.Handle(new GetWorkspaceQuery(Guid.NewGuid()), default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateWorkspace_ChangesNameAndPath()
    {
        var created = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand("Old", @"C:\old"), default);

        var handler = new UpdateWorkspaceCommandHandler(_db);
        var result = await handler.Handle(
            new UpdateWorkspaceCommand(created.Id, "New", @"C:\new"), default);

        result.Name.Should().Be("New");
        result.Path.Should().Be(@"C:\new");
        result.UpdatedAt.Should().BeOnOrAfter(result.CreatedAt);
    }

    [Fact]
    public async Task DeleteWorkspace_RemovesFromDatabase()
    {
        var created = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand("ToDelete", @"C:\delete"), default);

        await new DeleteWorkspaceCommandHandler(_db)
            .Handle(new DeleteWorkspaceCommand(created.Id), default);

        var remaining = await new ListWorkspacesQueryHandler(_db)
            .Handle(new ListWorkspacesQuery(), default);

        remaining.Should().BeEmpty();
    }
}
