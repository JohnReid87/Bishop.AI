using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Skills.GetSkillBootstrapInfo;
using Bishop.App.Tags.ListTags;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Skills;

public sealed class GetSkillBootstrapInfoQueryHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public GetSkillBootstrapInfoQueryHandlerTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static IMediator MediatorWith(IReadOnlyList<TagInfo> tags, IReadOnlyList<LaneInfo> lanes)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(tags);
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(lanes);
        return mediator;
    }

    [Fact]
    public async Task Handle_ReturnsBundleForExistingWorkspace()
    {
        // Arrange
        var name = U("MyRepo");
        var path = $@"C:\code\{name}";
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, path), default);

        var tags = new[] { new TagInfo("bug", "#ff0000"), new TagInfo("feature", "#00ff00") };
        var lanes = new[] { new LaneInfo("Backlog", 1), new LaneInfo("To Do", 2), new LaneInfo("Doing", 3), new LaneInfo("Done", 4) };
        var mediator = MediatorWith(tags, lanes);
        var handler = new GetSkillBootstrapInfoQueryHandler(_factory, mediator);

        // Act
        var result = await handler.Handle(new GetSkillBootstrapInfoQuery(workspace.Id), default);

        // Assert
        result.WorkspaceName.Should().Be(name);
        result.WorkspacePath.Should().Be(path);
        result.Tags.Should().BeEquivalentTo(tags);
        result.Lanes.Should().BeEquivalentTo(lanes);
    }

    [Fact]
    public async Task Handle_ThrowsWhenWorkspaceNotFound()
    {
        // Arrange
        var mediator = MediatorWith(Array.Empty<TagInfo>(), Array.Empty<LaneInfo>());
        var handler = new GetSkillBootstrapInfoQueryHandler(_factory, mediator);

        // Act
        var act = () => handler.Handle(new GetSkillBootstrapInfoQuery(Guid.NewGuid()), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
