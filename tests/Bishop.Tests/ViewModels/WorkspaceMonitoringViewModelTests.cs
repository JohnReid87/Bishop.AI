using Bishop.App.Git;
using Bishop.App.Services.Settings;
using Bishop.App.Workspaces.GetWorkspaceSkillRuns;
using Bishop.Core;
using Bishop.ViewModels;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class WorkspaceMonitoringViewModelTests
{
    private static readonly string[] TrackedSkills =
    [
        "bish-audit-docs",
        "bish-arch",
        "bish-tests",
        "bish-coverage",
        "bish-security",
    ];

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IGitCli _gitCli = Substitute.For<IGitCli>();
    private readonly IAppSettings _appSettings = Substitute.For<IAppSettings>();
    private readonly WorkspaceMonitoringViewModel _vm;

    public WorkspaceMonitoringViewModelTests()
    {
        _vm = new WorkspaceMonitoringViewModel(_mediator, _gitCli, _appSettings);
        _mediator
            .Send(Arg.Any<GetWorkspaceSkillRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>([]));
        _gitCli
            .GetCommitCountSinceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(0));
        _appSettings
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));
    }

    [Fact]
    public async Task LoadAsync_PopulatesOneRowPerTrackedSkill()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        _vm.Rows.Should().HaveCount(TrackedSkills.Length);
        _vm.Rows.Select(r => r.SkillName).Should().BeEquivalentTo(TrackedSkills);
    }

    [Fact]
    public async Task LoadAsync_SkillWithNoRun_ShowsNeverStatus()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        var row = _vm.Rows.First(r => r.SkillName == "bish-arch");
        row.LastRunText.Should().Be("Never");
        row.StatusDotColor.Should().Be("#c97a8a");
    }

    [Fact]
    public async Task LoadAsync_SkillWithRecentRun_ShowsCommitCount()
    {
        var workspaceId = Guid.NewGuid();
        var sha = "abc123";
        _mediator
            .Send(Arg.Is<GetWorkspaceSkillRunsQuery>(q => q.WorkspaceId == workspaceId), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
            [
                new WorkspaceSkillRun
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    SkillName = "bish-arch",
                    RecordedAt = DateTimeOffset.UtcNow.AddHours(-1),
                    GitSha = sha,
                }
            ]));
        _gitCli.GetCommitCountSinceAsync(sha, @"C:\fake", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(3));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        var row = _vm.Rows.First(r => r.SkillName == "bish-arch");
        row.CommitsSinceText.Should().Be("3");
        row.StatusDotColor.Should().Be("#4a9e6a");
    }

    [Fact]
    public async Task LoadAsync_ShaUnreachable_ShowsReauditStatus()
    {
        var workspaceId = Guid.NewGuid();
        var sha = "deadbeef";
        _mediator
            .Send(Arg.Is<GetWorkspaceSkillRunsQuery>(q => q.WorkspaceId == workspaceId), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
            [
                new WorkspaceSkillRun
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    SkillName = "bish-security",
                    RecordedAt = DateTimeOffset.UtcNow.AddDays(-2),
                    GitSha = sha,
                }
            ]));
        _gitCli.GetCommitCountSinceAsync(sha, @"C:\fake", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(null));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        var row = _vm.Rows.First(r => r.SkillName == "bish-security");
        row.CommitsSinceText.Should().Be("Re-audit");
        row.StatusDotColor.Should().Be("#c97a8a");
    }

    [Fact]
    public async Task LoadAsync_PassesWorkspaceIdToQuery()
    {
        var workspaceId = Guid.NewGuid();

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        await _mediator.Received(1).Send(
            Arg.Is<GetWorkspaceSkillRunsQuery>(q => q.WorkspaceId == workspaceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_AllClear_BadgeHidden()
    {
        _gitCli.GetCommitCountSinceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(0));
        var workspaceId = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetWorkspaceSkillRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
                TrackedSkills.Select(s => new WorkspaceSkillRun
                {
                    Id = Guid.NewGuid(), WorkspaceId = workspaceId,
                    SkillName = s, RecordedAt = DateTimeOffset.UtcNow, GitSha = "abc"
                }).ToList()));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        _vm.BadgeCount.Should().Be(0);
        _vm.BadgeIsVisible.Should().BeFalse();
        _vm.BadgeColor.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_NeverRunSkills_BadgeIsRedWithCorrectCount()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        _vm.BadgeIsVisible.Should().BeTrue();
        _vm.BadgeColor.Should().Be("#c97a8a");
        _vm.BadgeCount.Should().Be(TrackedSkills.Length);
        _vm.BadgeTooltip.Should().Be($"{TrackedSkills.Length} of 5 reviews need attention");
    }

    [Fact]
    public async Task LoadAsync_OnlyAmberSkills_BadgeIsAmber()
    {
        var workspaceId = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetWorkspaceSkillRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
                TrackedSkills.Select(s => new WorkspaceSkillRun
                {
                    Id = Guid.NewGuid(), WorkspaceId = workspaceId,
                    SkillName = s, RecordedAt = DateTimeOffset.UtcNow, GitSha = "abc"
                }).ToList()));
        _gitCli.GetCommitCountSinceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(15));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        _vm.BadgeIsVisible.Should().BeTrue();
        _vm.BadgeColor.Should().Be("#c4a85f");
        _vm.BadgeCount.Should().Be(TrackedSkills.Length);
    }

    [Fact]
    public async Task LoadAsync_MixOfRedAndAmber_BadgeIsRed()
    {
        var workspaceId = Guid.NewGuid();
        var archSha = "arch-sha";
        _mediator.Send(Arg.Any<GetWorkspaceSkillRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
            [
                new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-arch", RecordedAt = DateTimeOffset.UtcNow, GitSha = archSha },
                new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-tests", RecordedAt = DateTimeOffset.UtcNow, GitSha = "tests-sha" },
            ]));
        _gitCli.GetCommitCountSinceAsync(archSha, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(60));
        _gitCli.GetCommitCountSinceAsync(Arg.Is<string>(s => s != archSha), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(15));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        _vm.BadgeIsVisible.Should().BeTrue();
        _vm.BadgeColor.Should().Be("#c97a8a");
    }

    [Fact]
    public async Task LoadAsync_NoSavedModel_SeedsDefaultSonnet()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        _vm.Rows.Should().AllSatisfy(r =>
        {
            r.SelectedModelId.Should().Be("claude-sonnet-4-6");
            r.SelectedModelLabel.Should().Be("Sonnet 4.6 ▾");
        });
    }

    [Fact]
    public async Task LoadAsync_SavedOpusModel_SeedsOpusOnMatchingRow()
    {
        _appSettings
            .GetAsync("skill.bish-arch.last_model", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("claude-opus-4-7"));

        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        var row = _vm.Rows.First(r => r.SkillName == "bish-arch");
        row.SelectedModelId.Should().Be("claude-opus-4-7");
        row.SelectedModelLabel.Should().Be("Opus 4.7 ▾");
    }

    [Fact]
    public async Task SelectModel_PersistsChoiceToAppSettings()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");
        var row = _vm.Rows.First(r => r.SkillName == "bish-arch");

        row.SelectModelCommand.Execute("claude-haiku-4-5-20251001");

        await _appSettings.Received(1).SetAsync(
            "skill.bish-arch.last_model",
            "claude-haiku-4-5-20251001",
            Arg.Any<CancellationToken>());
    }
}
