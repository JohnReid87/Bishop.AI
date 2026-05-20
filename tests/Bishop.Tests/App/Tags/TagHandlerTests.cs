using Bishop.App.Tags.AddTag;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.App.Tags.RemoveTag;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;

namespace Bishop.Tests.App.Tags;

public sealed class TagHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;

    public TagHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = U("ws");
        return await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
    }

    // ── AddTagCommandHandler ─────────────────────────────────────────────────

    [Fact]
    public async Task AddTag_PersistsAndReturnsTag()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var handler = new AddTagCommandHandler(_db);

        // Act
        var result = await handler.Handle(new AddTagCommand(workspace.Id, "bug", "#FF0000"), default);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("bug");
        result.Colour.Should().Be("#FF0000");
        result.WorkspaceId.Should().Be(workspace.Id);
    }

    [Fact]
    public async Task AddTag_UsesDefaultColour_WhenColourIsNull()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var handler = new AddTagCommandHandler(_db);

        // Act
        var result = await handler.Handle(new AddTagCommand(workspace.Id, "feature"), default);

        // Assert
        result.Colour.Should().Be("#888888");
    }

    [Fact]
    public async Task AddTag_Throws_WhenTagNameAlreadyExistsInWorkspace()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var handler = new AddTagCommandHandler(_db);
        await handler.Handle(new AddTagCommand(workspace.Id, "duplicate"), default);

        // Act
        var act = async () => await handler.Handle(new AddTagCommand(workspace.Id, "duplicate"), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*duplicate*");
    }

    [Fact]
    public async Task AddTag_AllowsSameNameInDifferentWorkspaces()
    {
        // Arrange
        var workspaceA = await CreateWorkspaceAsync();
        var workspaceB = await CreateWorkspaceAsync();
        var handler = new AddTagCommandHandler(_db);
        await handler.Handle(new AddTagCommand(workspaceA.Id, "shared"), default);

        // Act
        var act = async () => await handler.Handle(new AddTagCommand(workspaceB.Id, "shared"), default);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── ListTagsByWorkspaceQueryHandler ──────────────────────────────────────

    [Fact]
    public async Task ListTags_ReturnsEmpty_WhenWorkspaceHasNoTags()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var handler = new ListTagsByWorkspaceQueryHandler(_db);

        // Act
        var result = await handler.Handle(new ListTagsByWorkspaceQuery(workspace.Id), default);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListTags_ReturnsTagsOrderedByName()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var add = new AddTagCommandHandler(_db);
        await add.Handle(new AddTagCommand(workspace.Id, "zebra"), default);
        await add.Handle(new AddTagCommand(workspace.Id, "alpha"), default);
        await add.Handle(new AddTagCommand(workspace.Id, "middle"), default);
        var handler = new ListTagsByWorkspaceQueryHandler(_db);

        // Act
        var result = await handler.Handle(new ListTagsByWorkspaceQuery(workspace.Id), default);

        // Assert
        result.Select(t => t.Name).Should().Equal("alpha", "middle", "zebra");
    }

    [Fact]
    public async Task ListTags_ExcludesTagsFromOtherWorkspaces()
    {
        // Arrange
        var workspaceA = await CreateWorkspaceAsync();
        var workspaceB = await CreateWorkspaceAsync();
        var add = new AddTagCommandHandler(_db);
        await add.Handle(new AddTagCommand(workspaceA.Id, "a-tag"), default);
        await add.Handle(new AddTagCommand(workspaceB.Id, "b-tag"), default);
        var handler = new ListTagsByWorkspaceQueryHandler(_db);

        // Act
        var result = await handler.Handle(new ListTagsByWorkspaceQuery(workspaceA.Id), default);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("a-tag");
    }

    // ── RemoveTagCommandHandler ──────────────────────────────────────────────

    [Fact]
    public async Task RemoveTag_DeletesTag()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var tag = await new AddTagCommandHandler(_db)
            .Handle(new AddTagCommand(workspace.Id, "to-remove"), default);
        var handler = new RemoveTagCommandHandler(_db);

        // Act
        await handler.Handle(new RemoveTagCommand(workspace.Id, "to-remove"), default);

        // Assert
        var remaining = await _db.Tags.FindAsync(tag.Id);
        remaining.Should().BeNull();
    }

    [Fact]
    public async Task RemoveTag_Throws_WhenTagNotFound()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var handler = new RemoveTagCommandHandler(_db);

        // Act
        var act = async () => await handler.Handle(new RemoveTagCommand(workspace.Id, "nonexistent"), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public async Task RemoveTag_Throws_WhenTagBelongsToOtherWorkspace()
    {
        // Arrange
        var workspaceA = await CreateWorkspaceAsync();
        var workspaceB = await CreateWorkspaceAsync();
        await new AddTagCommandHandler(_db)
            .Handle(new AddTagCommand(workspaceA.Id, "cross-ws"), default);
        var handler = new RemoveTagCommandHandler(_db);

        // Act
        var act = async () => await handler.Handle(new RemoveTagCommand(workspaceB.Id, "cross-ws"), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cross-ws*");
    }
}
