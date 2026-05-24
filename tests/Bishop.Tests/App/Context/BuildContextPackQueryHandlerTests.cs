using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Context.ContextPack;
using Bishop.App.Context.ContextPack.Providers;
using Bishop.App.Git;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Context;

public sealed class BuildContextPackQueryHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public BuildContextPackQueryHandlerTests(DbFixture fixture) => _factory = fixture.Factory;

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = U("ws");
        return await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, Path.Combine(Path.GetTempPath(), name)), default);
    }

    private ISender CreateSender()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new ListLanesByWorkspaceQueryHandler()
                .Handle(call.ArgAt<ListLanesByWorkspaceQuery>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new ListTagsByWorkspaceQueryHandler()
                .Handle(call.ArgAt<ListTagsByWorkspaceQuery>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new GetCardByNumberQueryHandler(_factory)
                .Handle(call.ArgAt<GetCardByNumberQuery>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    private IGitCli StubGitCli()
    {
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("main"));
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GetRecentCommitsResult>(new GetRecentCommitsResult.Success(
                new List<CommitInfo>
                {
                    new("abc1234", "abc1234567890", "feat: add x", "", DateTimeOffset.UtcNow, false)
                },
                "origin/main")));
        return git;
    }

    private static BuildContextPackQueryHandler CreateHandler(
        ISender sender,
        IGitCli git,
        params IContextProvider[] providers) => new(providers, git, sender);

    [Fact]
    public async Task ReturnsAllFourBlocks_ForWorkOnCardProvider()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var sender = CreateSender();
        var addedCard = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "My card", "Body", TagName: null), default);

        var handler = CreateHandler(sender, StubGitCli(), new WorkOnCardContextProvider());

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery("work-on-card", workspace, new ContextPackArgs(addedCard.Number)),
            default);

        // Assert
        pack.Workspace.Name.Should().Be(workspace.Name);
        pack.Workspace.Lanes.Should().Contain(SystemLaneNames.ToDo);
        pack.Workspace.Tags.Should().NotBeEmpty();

        pack.Git.Branch.Should().Be("main");
        pack.Git.Commits.Should().HaveCount(1);

        pack.SkillSpecific.Should().NotBeNull();

        pack.Conventions.Should().ContainKey("Shell selection");
        pack.Conventions.Should().ContainKey("Commit-reference convention");
        pack.Conventions.Should().ContainKey("Card model");
    }

    [Fact]
    public async Task ReturnsAllFourBlocks_ForAutoCardProvider()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var sender = CreateSender();
        var addedCard = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Another", "Body", TagName: null), default);

        var handler = CreateHandler(sender, StubGitCli(), new AutoCardContextProvider());

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery("auto-card", workspace, new ContextPackArgs(addedCard.Number)),
            default);

        // Assert
        pack.Conventions.Should().ContainKey("Shell selection");
        pack.Conventions.Should().ContainKey("Commit-reference convention");
        pack.Conventions.Should().ContainKey("Auto-card permission contract");
        pack.Conventions.Should().ContainKey("Card model");
    }

    [Fact]
    public async Task UnknownSkillName_ThrowsWithKnownProviderList()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var handler = CreateHandler(CreateSender(), StubGitCli(), new WorkOnCardContextProvider());

        // Act
        var act = () => handler.Handle(
            new BuildContextPackQuery("does-not-exist", workspace, new ContextPackArgs(null)),
            default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does-not-exist*work-on-card*");
    }

    [Fact]
    public async Task UnknownSectionName_FailsFast()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var badProvider = new TypoSectionProvider();
        var handler = CreateHandler(CreateSender(), StubGitCli(), badProvider);

        // Act
        var act = () => handler.Handle(
            new BuildContextPackQuery(badProvider.SkillName, workspace, new ContextPackArgs(null)),
            default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Definitely Not A Real Section*");
    }

    [Fact]
    public async Task ProviderCardLookup_ThrowsWhenCardMissing()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var handler = CreateHandler(CreateSender(), StubGitCli(), new WorkOnCardContextProvider());

        // Act
        var act = () => handler.Handle(
            new BuildContextPackQuery("work-on-card", workspace, new ContextPackArgs(9999)),
            default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Card #9999*");
    }

    [Fact]
    public async Task NoCardArg_SkillSpecificContainsNullCard()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var handler = CreateHandler(CreateSender(), StubGitCli(), new WorkOnCardContextProvider());

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery("work-on-card", workspace, new ContextPackArgs(null)),
            default);

        // Assert
        pack.SkillSpecific.Should().NotBeNull();
    }

    private sealed class TypoSectionProvider : IContextProvider
    {
        public string SkillName => "typo-test";
        public IReadOnlyList<string> RequiredSections { get; } = new[] { "Definitely Not A Real Section" };
        public Task<object?> BuildSkillSpecificAsync(
            ContextPackArgs args, Workspace workspace, ISender mediator, CancellationToken cancellationToken)
            => Task.FromResult<object?>(null);
    }
}
