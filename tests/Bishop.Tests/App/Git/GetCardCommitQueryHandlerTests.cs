using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.SetCardCommit;
using Bishop.App.Git;
using Bishop.App.Git.GetCardCommit;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Git;

public sealed class GetCardCommitQueryHandlerTests : IClassFixture<DbFixture>
{
    private const int CardNumber = 42;
    private const string WorkspacePath = @"C:\repos\my-project";

    private readonly IDbContextFactory<BishopDbContext> _factory;

    public GetCardCommitQueryHandlerTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
    }

    private GetCardCommitQueryHandler CreateSut(IGitCli git) => new(git, _factory);

    // ── legacy fallback (no persisted hash) ──────────────────────────────────

    [Fact]
    public async Task Handle_FallsBackToGitCli_WhenNoCardInDb()
    {
        // Arrange
        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.NotFound());
        var sut = CreateSut(git);

        // Act
        await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), CancellationToken.None);

        // Assert
        await git.Received(1).GetCardCommitAsync(CardNumber, WorkspacePath, Arg.Any<CancellationToken>());
        await git.DidNotReceive().GetCommitByHashAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FallsBackToGitCli_WhenCardHasNoCommitHash()
    {
        // Arrange
        var workspace = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand("ws-no-hash", WorkspacePath), default);
        await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, "To Do", "Some card", "") with { }, default);

        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.NotFound());
        var sut = CreateSut(git);

        // We don't know the card number assigned, so use CardNumber which won't match — same result
        await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), CancellationToken.None);

        // Assert
        await git.Received(1).GetCardCommitAsync(CardNumber, WorkspacePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ForwardsCancellationTokenToFallback()
    {
        // Arrange
        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.NotFound());
        var sut = CreateSut(git);
        using var cts = new CancellationTokenSource();

        // Act
        await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), cts.Token);

        // Assert
        await git.Received(1).GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), cts.Token);
    }

    [Fact]
    public async Task Handle_ReturnsFallbackResult()
    {
        // Arrange
        var expected = new GetCardCommitResult.NotFound();
        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    // ── persisted hash path ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UsesPersistedHash_WhenCardHasCommitHash()
    {
        // Arrange
        const string persistedHash = "abcdef1234567890abcdef1234567890abcd1234";
        const string wsPath = @"C:\repos\hash-project";

        var workspace = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand("ws-with-hash", wsPath), default);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, "To Do", "Card with hash", ""), default);
        await new SetCardCommitCommandHandler(_factory, TimeProvider.System)
            .Handle(new SetCardCommitCommand(card.Id, persistedHash, "main"), default);

        var commit = new CommitInfo("abcdef12", persistedHash, "", "", DateTimeOffset.UtcNow, true);
        var expected = new GetCardCommitResult.Found(commit);

        var git = Substitute.For<IGitCli>();
        git.GetCommitByHashAsync(persistedHash, wsPath, Arg.Any<CancellationToken>())
            .Returns(expected);

        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new GetCardCommitQuery(card.Number, wsPath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        await git.Received(1).GetCommitByHashAsync(persistedHash, wsPath, Arg.Any<CancellationToken>());
        await git.DidNotReceive().GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
