using Bishop.App.Batches.CreateBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Git;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class CreateBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly IGitCli _git;

    public CreateBatchCommandHandlerTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
        _git = Substitute.For<IGitCli>();
        _git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("main");
        _git.CreateWorktreeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = $"batchtest-{Guid.NewGuid():N}"[..20];
        return await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
    }

    private CreateBatchCommandHandler MakeHandler() =>
        new(_git, _factory, TimeProvider.System);

    [Fact]
    public async Task Handle_NoCards_CreatesBatchWithWorktree()
    {
        // Arrange
        var ws = await CreateWorkspaceAsync();
        var cmd = new CreateBatchCommand(
            ws.Id, ws.Path, "Sprint 1", "bishop/sprint-1", null,
            @"C:\worktrees\sprint-1", [], null, null);

        // Act
        var result = await MakeHandler().Handle(cmd, default);

        // Assert
        result.Batch.Name.Should().Be("Sprint 1");
        result.Batch.BranchName.Should().Be("bishop/sprint-1");
        result.Batch.BaseBranch.Should().Be("main");
        result.Batch.Status.Should().Be(BatchStatus.Open);
        result.CardCount.Should().Be(0);
        await _git.Received(1).GetCurrentBranchAsync(ws.Path, Arg.Any<CancellationToken>());
        await _git.Received(1).CreateWorktreeAsync(ws.Path, "bishop/sprint-1", "main", @"C:\worktrees\sprint-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithBaseBranch_SkipsGetCurrentBranch()
    {
        // Arrange
        var ws = await CreateWorkspaceAsync();
        var cmd = new CreateBatchCommand(
            ws.Id, ws.Path, "Hotfix", "bishop/hotfix", "develop",
            @"C:\worktrees\hotfix", [], null, null);

        // Act
        var result = await MakeHandler().Handle(cmd, default);

        // Assert
        result.Batch.BaseBranch.Should().Be("develop");
        await _git.DidNotReceive().GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithCardNumbers_AssignsCards()
    {
        // Arrange
        var ws = await CreateWorkspaceAsync();
        var addHandler = new AddCardCommandHandler(_factory);
        var card1 = await addHandler.Handle(new AddCardCommand(ws.Id, SystemLaneNames.ToDo, "Card A"), default);
        var card2 = await addHandler.Handle(new AddCardCommand(ws.Id, SystemLaneNames.ToDo, "Card B"), default);

        var cmd = new CreateBatchCommand(
            ws.Id, ws.Path, "With Cards", "bishop/with-cards", "main",
            @"C:\worktrees\with-cards", [card1.Number, card2.Number], null, null);

        // Act
        var result = await MakeHandler().Handle(cmd, default);

        // Assert
        result.CardCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_MissingCardNumber_ThrowsWithMissingNumbers()
    {
        // Arrange
        var ws = await CreateWorkspaceAsync();
        var cmd = new CreateBatchCommand(
            ws.Id, ws.Path, "Bad", "bishop/bad", "main",
            @"C:\worktrees\bad", [999], null, null);

        // Act
        var act = () => MakeHandler().Handle(cmd, default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*#999*");
    }

    [Fact]
    public async Task Handle_PersistsModelOnBatch()
    {
        // Arrange
        var ws = await CreateWorkspaceAsync();
        var cmd = new CreateBatchCommand(
            ws.Id, ws.Path, "Modelled", "bishop/modelled", null,
            @"C:\worktrees\modelled", [], null, null, Model: "claude-opus-4-7");

        // Act
        var result = await MakeHandler().Handle(cmd, default);

        // Assert
        result.Batch.Model.Should().Be("claude-opus-4-7");
        await using var db = await _factory.CreateDbContextAsync();
        var saved = await db.Batches.FindAsync(result.Batch.Id);
        saved!.Model.Should().Be("claude-opus-4-7");
    }

    [Fact]
    public async Task Handle_DefaultModel_PersistsDefaultModelId()
    {
        // Arrange
        var ws = await CreateWorkspaceAsync();
        var cmd = new CreateBatchCommand(
            ws.Id, ws.Path, "Default", "bishop/default", null,
            @"C:\worktrees\default", [], null, null);

        // Act
        var result = await MakeHandler().Handle(cmd, default);

        // Assert
        result.Batch.Model.Should().Be(Bishop.App.Skills.SkillModelOptions.DefaultModelId);
    }

    [Fact]
    public async Task Handle_WithMultipleCards_AssignsAllCardsInDb()
    {
        // Arrange
        var ws = await CreateWorkspaceAsync();
        var addHandler = new AddCardCommandHandler(_factory);
        var card1 = await addHandler.Handle(new AddCardCommand(ws.Id, SystemLaneNames.ToDo, "Card A"), default);
        var card2 = await addHandler.Handle(new AddCardCommand(ws.Id, SystemLaneNames.ToDo, "Card B"), default);
        var card3 = await addHandler.Handle(new AddCardCommand(ws.Id, SystemLaneNames.ToDo, "Card C"), default);

        var cmd = new CreateBatchCommand(
            ws.Id, ws.Path, "Multi Card", "bishop/multi-card", "main",
            @"C:\worktrees\multi-card", [card1.Number, card2.Number, card3.Number], null, null);

        // Act
        var result = await MakeHandler().Handle(cmd, default);

        // Assert
        result.CardCount.Should().Be(3);
        await using var db = await _factory.CreateDbContextAsync();
        var assigned = await db.Cards
            .Where(c => c.BatchId == result.Batch.Id)
            .CountAsync();
        assigned.Should().Be(3);
    }

    [Fact]
    public async Task Handle_TagFilter_AssignsMatchingCards()
    {
        // Arrange
        var ws = await CreateWorkspaceAsync();
        var addHandler = new AddCardCommandHandler(_factory);
        var tagged = await addHandler.Handle(new AddCardCommand(ws.Id, SystemLaneNames.ToDo, "Tagged", TagName: "feature"), default);
        await addHandler.Handle(new AddCardCommand(ws.Id, SystemLaneNames.ToDo, "Untagged"), default);

        var cmd = new CreateBatchCommand(
            ws.Id, ws.Path, "Tag Batch", "bishop/tag-batch", "main",
            @"C:\worktrees\tag-batch", [], "feature", null);

        // Act
        var result = await MakeHandler().Handle(cmd, default);

        // Assert
        result.CardCount.Should().Be(1);
    }
}
