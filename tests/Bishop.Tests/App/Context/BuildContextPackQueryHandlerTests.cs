using System.Text.Json;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Context.ContextPack;
using Bishop.App.Context.ContextPack.Providers;
using Bishop.App.Git;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Tags.ListTags;
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
        sender.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new ListTagsQueryHandler()
                .Handle(call.ArgAt<ListTagsQuery>(0), call.ArgAt<CancellationToken>(1)));
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

    [Theory]
    [InlineData("coverage", new[] { "Shell selection", "Card model", "Findings Recording Procedure" })]
    [InlineData("arch", new[] { "Shell selection", "Card model", "Findings Recording Procedure" })]
    [InlineData("security", new[] { "Shell selection", "Card model", "Findings Recording Procedure" })]
    [InlineData("tests", new[] { "Shell selection", "Card model", "Findings Recording Procedure" })]
    [InlineData("dead-code", new[] { "Shell selection", "Card model", "Findings Recording Procedure" })]
    [InlineData("audit-docs", new[] { "Shell selection", "Findings Recording Procedure" })]
    [InlineData("grill-cards", new[] { "Shell selection", "Card Granularity Rules", "Task List Preview Format", "Card Push Procedure", "Source Card Closing Prompt" })]
    [InlineData("spec-cards", new[] { "Shell selection", "Card Granularity Rules", "Task List Preview Format", "Card Push Procedure", "Source Card Closing Prompt" })]
    [InlineData("grill-docs", new[] { "Shell selection" })]
    [InlineData("triage", new[] { "Shell selection", "Card Push Procedure", "Source Card Closing Prompt" })]
    [InlineData("chat", new[] { "Shell selection", "Card Granularity Rules", "Task List Preview Format", "Card Push Procedure" })]
    public async Task NewProviders_DeliverExpectedConventionKeys(string skillName, string[] expectedKeys)
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var provider = CreateProviderBySkillName(skillName);
        var handler = CreateHandler(CreateSender(), StubGitCli(), provider);

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery(skillName, workspace, new ContextPackArgs(null)),
            default);

        // Assert
        foreach (var key in expectedKeys)
            pack.Conventions.Should().ContainKey(key, $"provider '{skillName}' declared RequiredSection '{key}'");

        pack.Conventions.Should().HaveCount(expectedKeys.Length);
    }

    [Theory]
    [InlineData("grill-cards")]
    [InlineData("spec-cards")]
    [InlineData("triage")]
    [InlineData("chat")]
    public async Task CardAwareProviders_LoadCardWhenArgProvided(string skillName)
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var addedCard = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Test card", "Body", TagName: null), default);

        var provider = CreateProviderBySkillName(skillName);
        var handler = CreateHandler(CreateSender(), StubGitCli(), provider);

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery(skillName, workspace, new ContextPackArgs(addedCard.Number)),
            default);

        // Assert
        pack.SkillSpecific.Should().NotBeNull();
    }

    [Theory]
    [InlineData("coverage")]
    [InlineData("arch")]
    [InlineData("security")]
    [InlineData("tests")]
    [InlineData("audit-docs")]
    [InlineData("grill-docs")]
    [InlineData("dead-code")]
    public async Task WorkspaceOnlyProviders_SkillSpecificIsNull(string skillName)
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var provider = CreateProviderBySkillName(skillName);
        var handler = CreateHandler(CreateSender(), StubGitCli(), provider);

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery(skillName, workspace, new ContextPackArgs(null)),
            default);

        // Assert
        pack.SkillSpecific.Should().BeNull();
    }

    private static IContextProvider CreateProviderBySkillName(string skillName) => skillName switch
    {
        "coverage" => new CoverageContextProvider(),
        "arch" => new ArchContextProvider(),
        "security" => new SecurityContextProvider(),
        "tests" => new TestsContextProvider(),
        "audit-docs" => new AuditDocsContextProvider(),
        "grill-cards" => new GrillCardsContextProvider(),
        "spec-cards" => new SpecCardsContextProvider(),
        "grill-docs" => new GrillDocsContextProvider(),
        "triage" => new TriageContextProvider(),
        "chat" => new ChatContextProvider(),
        "auto-card" => new AutoCardContextProvider(),
        "work-on-card" => new WorkOnCardContextProvider(),
        "dead-code" => new DeadCodeContextProvider(),
        _ => throw new ArgumentOutOfRangeException(nameof(skillName), skillName, null)
    };

    [Theory]
    [InlineData("work-on-card")]
    [InlineData("auto-card")]
    [InlineData("grill-cards")]
    [InlineData("spec-cards")]
    [InlineData("triage")]
    [InlineData("chat")]
    public async Task CardProviders_NoRelatedSection_EmitsEmptyRelatedCards(string skillName)
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var sender = CreateSender();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "My card", "### Why\nNo related section", TagName: null), default);

        var handler = CreateHandler(sender, StubGitCli(), CreateProviderBySkillName(skillName));

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery(skillName, workspace, new ContextPackArgs(card.Number)), default);

        // Assert
        var root = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(pack.SkillSpecific));
        root.GetProperty("relatedCards").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("work-on-card")]
    [InlineData("auto-card")]
    [InlineData("grill-cards")]
    [InlineData("spec-cards")]
    [InlineData("triage")]
    [InlineData("chat")]
    public async Task CardProviders_WithRelatedSection_LoadsReferencedCards(string skillName)
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var sender = CreateSender();
        var related = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Related card", "", TagName: null), default);
        var source = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Source card",
                $"### Why\nSomething\n### Related\n- #{related.Number}", TagName: null), default);

        var handler = CreateHandler(sender, StubGitCli(), CreateProviderBySkillName(skillName));

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery(skillName, workspace, new ContextPackArgs(source.Number)), default);

        // Assert
        var root = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(pack.SkillSpecific));
        var arr = root.GetProperty("relatedCards");
        arr.GetArrayLength().Should().Be(1);
        arr[0].GetProperty("number").GetInt32().Should().Be(related.Number);
        arr[0].GetProperty("title").GetString().Should().Be("Related card");
    }

    [Fact]
    public async Task CardProvider_MissingRelatedRef_SkippedSilently()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var sender = CreateSender();
        var source = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Source card",
                "### Why\nSomething\n### Related\n- #9999", TagName: null), default);

        var handler = CreateHandler(sender, StubGitCli(), new WorkOnCardContextProvider());

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery("work-on-card", workspace, new ContextPackArgs(source.Number)), default);

        // Assert
        var root = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(pack.SkillSpecific));
        root.GetProperty("relatedCards").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ContextMd_WhenFileExists_ReadsContent()
    {
        // Arrange
        var name = U("ctx");
        var dir = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "CONTEXT.md"), "# My context");
            var workspace = await new CreateWorkspaceCommandHandler(_factory)
                .Handle(new CreateWorkspaceCommand(name, dir), default);
            var handler = CreateHandler(CreateSender(), StubGitCli(), new WorkOnCardContextProvider());

            // Act
            var pack = await handler.Handle(
                new BuildContextPackQuery("work-on-card", workspace, new ContextPackArgs(null)),
                default);

            // Assert
            pack.Workspace.ContextMd.Should().Be("# My context");
            pack.Workspace.ContextMdTruncated.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ContextMd_WhenFileExceedsMaxBytes_ReturnsNullAndTruncatedTrue()
    {
        // Arrange
        var name = U("ctx");
        var dir = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(dir);
        try
        {
            var bigContent = new string('x', BuildContextPackQueryHandler.ContextMdMaxBytes + 1);
            File.WriteAllText(Path.Combine(dir, "CONTEXT.md"), bigContent);
            var workspace = await new CreateWorkspaceCommandHandler(_factory)
                .Handle(new CreateWorkspaceCommand(name, dir), default);
            var handler = CreateHandler(CreateSender(), StubGitCli(), new WorkOnCardContextProvider());

            // Act
            var pack = await handler.Handle(
                new BuildContextPackQuery("work-on-card", workspace, new ContextPackArgs(null)),
                default);

            // Assert
            pack.Workspace.ContextMd.Should().BeNull();
            pack.Workspace.ContextMdTruncated.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ContextMd_WhenFileIsLocked_ReturnsNull()
    {
        // Arrange
        var name = U("ctx");
        var dir = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(dir);
        var contextPath = Path.Combine(dir, "CONTEXT.md");
        File.WriteAllText(contextPath, "locked content");
        try
        {
            ContextPack pack;
            using (var lockStream = new FileStream(contextPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var workspace = await new CreateWorkspaceCommandHandler(_factory)
                    .Handle(new CreateWorkspaceCommand(name, dir), default);
                var handler = CreateHandler(CreateSender(), StubGitCli(), new WorkOnCardContextProvider());
                pack = await handler.Handle(
                    new BuildContextPackQuery("work-on-card", workspace, new ContextPackArgs(null)),
                    default);
            }

            // Assert
            pack.Workspace.ContextMd.Should().BeNull();
            pack.Workspace.ContextMdTruncated.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GitBlock_WhenGetBranchThrows_BranchIsNull()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new InvalidOperationException("Not a git repo"));
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GetRecentCommitsResult>(new GetRecentCommitsResult.NoCommits()));
        var handler = CreateHandler(CreateSender(), git, new WorkOnCardContextProvider());

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery("work-on-card", workspace, new ContextPackArgs(null)),
            default);

        // Assert
        pack.Git.Branch.Should().BeNull();
        pack.Git.Commits.Should().BeEmpty();
    }

    [Fact]
    public async Task GitBlock_WhenNoCommitsResult_CommitsIsEmpty()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("main"));
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GetRecentCommitsResult>(new GetRecentCommitsResult.NoCommits()));
        var handler = CreateHandler(CreateSender(), git, new WorkOnCardContextProvider());

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery("work-on-card", workspace, new ContextPackArgs(null)),
            default);

        // Assert
        pack.Git.Branch.Should().Be("main");
        pack.Git.Commits.Should().BeEmpty();
    }

    [Fact]
    public async Task GitBlock_WhenNotAGitRepoResult_CommitsIsEmpty()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("main"));
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GetRecentCommitsResult>(new GetRecentCommitsResult.NotAGitRepo()));
        var handler = CreateHandler(CreateSender(), git, new WorkOnCardContextProvider());

        // Act
        var pack = await handler.Handle(
            new BuildContextPackQuery("work-on-card", workspace, new ContextPackArgs(null)),
            default);

        // Assert
        pack.Git.Branch.Should().Be("main");
        pack.Git.Commits.Should().BeEmpty();
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
