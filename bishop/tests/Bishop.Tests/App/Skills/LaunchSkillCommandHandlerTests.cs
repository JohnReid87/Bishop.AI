using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Services.Terminal;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bishop.Tests.App.Skills;

public sealed class LaunchSkillCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;

    public LaunchSkillCommandHandlerTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private LaunchSkillCommandHandler CreateHandler(ITerminalLauncher launcher, IWorkspaceContextSeeder? seeder = null) =>
        new(launcher, seeder ?? Substitute.For<IWorkspaceContextSeeder>(), _factory);

    private async Task<Batch> SeedBatchAsync(BatchStatus status, string worktreePath)
    {
        await using var db = await _factory.CreateDbContextAsync(default);
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _wsId,
            Name = "batch-" + Guid.NewGuid().ToString("N")[..8],
            BranchName = "bishop/x",
            BaseBranch = "main",
            WorktreePath = worktreePath,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        return batch;
    }

    private async Task<BatchStatus> ReloadStatusAsync(Guid batchId)
    {
        await using var db = await _factory.CreateDbContextAsync(default);
        return (await db.Batches.FirstAsync(b => b.Id == batchId)).Status;
    }

    [Fact]
    public async Task Handle_ReturnsTrue_WhenLauncherSucceeds()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(true);
        var handler = CreateHandler(launcher);

        // Act
        var result = await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenLauncherReturnsFalse()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(false);
        var handler = CreateHandler(launcher);

        // Act
        var result = await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForwardsAllArgumentsToLauncher()
    {
        // Arrange
        var snap = new TerminalSnap(0, 0, 800, 600);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = CreateHandler(launcher);

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "/bish-work-on-card 42", snap, "claude-opus-4-7"), default);

        // Assert
        launcher.Received(1).Launch(@"C:\workspace", "/bish-work-on-card 42", snap, "claude-opus-4-7");
    }

    [Fact]
    public async Task Handle_ForwardsNullSnapAndModelId_WhenNotProvided()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = CreateHandler(launcher);

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        launcher.Received(1).Launch(@"C:\workspace", "code .", null, null);
    }

    [Fact]
    public async Task Handle_CallsSeedAsync_WithWorkspacePath()
    {
        // Arrange
        var seeder = Substitute.For<IWorkspaceContextSeeder>();
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = CreateHandler(launcher, seeder);
        var ct = new CancellationToken();

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), ct);

        // Assert
        await seeder.Received(1).SeedAsync(@"C:\workspace", ct);
    }

    [Fact]
    public async Task Handle_WhenSeedAsyncThrows_PropagatesExceptionAndDoesNotLaunch()
    {
        // Arrange
        var seeder = Substitute.For<IWorkspaceContextSeeder>();
        seeder.SeedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("seed failed")));
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = CreateHandler(launcher, seeder);

        // Act
        var act = async () => await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("seed failed");
        launcher.DidNotReceive().Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Handle_WhenLaunchThrows_PropagatesException()
    {
        // Arrange
        var seeder = Substitute.For<IWorkspaceContextSeeder>();
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>())
            .Throws(new InvalidOperationException("launch failed"));
        var handler = CreateHandler(launcher, seeder);

        // Act
        var act = async () => await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("launch failed");
    }

    // ── Batch worktree routing (card #1114) ──────────────────────────────────

    [Fact]
    public async Task Handle_LaunchesInWorktree_WhenCardBelongsToWorkingBatch()
    {
        // Arrange
        var worktree = CreateTempWorktree();
        var batch = await SeedBatchAsync(BatchStatus.Working, worktree);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = CreateHandler(launcher);

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "/bish-work-on-card 42", BatchId: batch.Id), default);

        // Assert
        launcher.Received(1).Launch(worktree, "/bish-work-on-card 42", null, null);
    }

    [Fact]
    public async Task Handle_FlipsOpenBatchToWorking_OnFirstLaunch()
    {
        // Arrange
        var worktree = CreateTempWorktree();
        var batch = await SeedBatchAsync(BatchStatus.Open, worktree);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = CreateHandler(launcher);

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "/bish-work-on-card 42", BatchId: batch.Id), default);

        // Assert
        (await ReloadStatusAsync(batch.Id)).Should().Be(BatchStatus.Working);
        launcher.Received(1).Launch(worktree, "/bish-work-on-card 42", null, null);
    }

    [Fact]
    public async Task Handle_LeavesWorkingBatchUntouched_OnSubsequentLaunch()
    {
        // Arrange
        var worktree = CreateTempWorktree();
        var batch = await SeedBatchAsync(BatchStatus.Working, worktree);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = CreateHandler(launcher);

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "/bish-work-on-card 42", BatchId: batch.Id), default);

        // Assert
        (await ReloadStatusAsync(batch.Id)).Should().Be(BatchStatus.Working);
    }

    [Fact]
    public async Task Handle_FallsBackToWorkspaceRoot_WhenBatchClosed()
    {
        // Arrange
        var worktree = CreateTempWorktree();
        var batch = await SeedBatchAsync(BatchStatus.Closed, worktree);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = CreateHandler(launcher);

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "/bish-work-on-card 42", BatchId: batch.Id), default);

        // Assert
        launcher.Received(1).Launch(@"C:\workspace", "/bish-work-on-card 42", null, null);
    }

    [Fact]
    public async Task Handle_FallsBackToWorkspaceRoot_WhenWorktreeMissing()
    {
        // Arrange
        var missing = Path.Combine(Path.GetTempPath(), "bishop-no-worktree-" + Guid.NewGuid().ToString("N")[..8]);
        var batch = await SeedBatchAsync(BatchStatus.Working, missing);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = CreateHandler(launcher);

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "/bish-work-on-card 42", BatchId: batch.Id), default);

        // Assert
        launcher.Received(1).Launch(@"C:\workspace", "/bish-work-on-card 42", null, null);
    }

    [Fact]
    public async Task Handle_FallsBackToWorkspaceRoot_WhenBatchNotFound()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = CreateHandler(launcher);

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code .", BatchId: Guid.NewGuid()), default);

        // Assert
        launcher.Received(1).Launch(@"C:\workspace", "code .", null, null);
    }

    private static string CreateTempWorktree()
    {
        var path = Path.Combine(Path.GetTempPath(), "bishop-worktree-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
