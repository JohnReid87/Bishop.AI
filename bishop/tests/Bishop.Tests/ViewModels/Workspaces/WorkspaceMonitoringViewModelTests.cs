using Bishop.App.Git.GetCommitCountSince;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Workspaces.GetWorkspaceSkillRuns;
using Bishop.Core;
using Bishop.Core.Skills;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Workspaces;

public class WorkspaceMonitoringViewModelTests
{
    // Installed review/analysis skills (Code / Tests / Review categories) drive the
    // Monitoring rows; declaration order matches the VM's category-then-name ordering.
    private static readonly InstalledSkill[] InstalledSkills =
    [
        new("bish-arch", "", ["workspace"], "/bish-arch", Category: SkillCategory.Code),
        new("bish-dead-code", "", ["workspace"], "/bish-dead-code", Category: SkillCategory.Code),
        new("bish-security", "", ["workspace"], "/bish-security", Category: SkillCategory.Code),
        new("bish-coverage", "", ["workspace"], "/bish-coverage", Category: SkillCategory.Tests),
        new("bish-tests", "", ["workspace"], "/bish-tests", Category: SkillCategory.Tests),
        new("bish-audit-docs", "", ["workspace"], "/bish-audit-docs", Category: SkillCategory.Review),
        new("bish-review-batch", "", ["workspace"], "/bish-review-batch", Category: SkillCategory.Review),
        // Non-review skills must not appear in Monitoring.
        new("bish-spec-cards", "", ["workspace"], "/bish-spec-cards", Category: SkillCategory.Discuss),
    ];

    private static readonly string[] TrackedSkills =
        InstalledSkills.Where(s => s.Category.IsMonitored()).Select(s => s.Name).ToArray();

    private readonly ISender _mediator = Substitute.For<ISender>();
    private readonly WorkspaceMonitoringViewModel _vm;

    public WorkspaceMonitoringViewModelTests()
    {
        _vm = new WorkspaceMonitoringViewModel(_mediator, TimeProvider.System);
        _mediator
            .Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<InstalledSkill>>(InstalledSkills));
        _mediator
            .Send(Arg.Any<GetWorkspaceSkillRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>([]));
        _mediator
            .Send(Arg.Any<GetCommitCountSinceQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(0));
    }

    [Fact]
    public async Task LoadAsync_PopulatesOneRowPerTrackedSkill()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        _vm.Rows.Should().HaveCount(TrackedSkills.Length);
        _vm.Rows.Select(r => r.SkillName).Should().BeEquivalentTo(TrackedSkills);
    }

    [Fact]
    public async Task LoadAsync_TracksReviewCategorySkills_AndExcludesNonReviewSkills()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        var names = _vm.Rows.Select(r => r.SkillName).ToList();
        names.Should().Contain(new[] { "bish-arch", "bish-coverage", "bish-audit-docs", "bish-review-batch" });
        names.Should().NotContain("bish-spec-cards");
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
        _mediator.Send(
                Arg.Is<GetCommitCountSinceQuery>(q => q.GitSha == sha && q.WorkspacePath == @"C:\fake"),
                Arg.Any<CancellationToken>())
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
        _mediator.Send(
                Arg.Is<GetCommitCountSinceQuery>(q => q.GitSha == sha),
                Arg.Any<CancellationToken>())
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
        _mediator.Send(Arg.Any<GetCommitCountSinceQuery>(), Arg.Any<CancellationToken>())
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
        _vm.BadgeTooltip.Should().Be($"{TrackedSkills.Length} of {TrackedSkills.Length} reviews need attention");
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
        _mediator.Send(Arg.Any<GetCommitCountSinceQuery>(), Arg.Any<CancellationToken>())
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
        _mediator.Send(Arg.Is<GetCommitCountSinceQuery>(q => q.GitSha == archSha), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(60));
        _mediator.Send(Arg.Is<GetCommitCountSinceQuery>(q => q.GitSha != archSha), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(15));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        _vm.BadgeIsVisible.Should().BeTrue();
        _vm.BadgeColor.Should().Be("#c97a8a");
    }

    [Fact]
    public async Task LoadAsync_NoSkillMetadata_DefaultsToSonnet()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");

        _vm.Rows.Should().AllSatisfy(r =>
        {
            r.SelectedModelId.Should().Be("claude-sonnet-4-6");
            r.SelectedModelLabel.Should().Be("Sonnet 4.6 ▾");
        });
    }

    [Fact]
    public async Task RowViewReport_PropagatesUriOnViewReportRequestedEvent()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\myrepo");
        Uri? captured = null;
        _vm.ViewReportRequested += uri => captured = uri;

        var coverageRow = _vm.Rows.First(r => r.SkillName == "bish-coverage");
        coverageRow.ViewReportCommand.Execute(null);

        captured.Should().Be(new Uri(@"C:\myrepo\TestResults\coverage-report\index.html"));
    }

[Fact]
    public async Task LoadAsync_BishTestsWithTwoProjects_EmitsTwoRows()
    {
        var workspaceId = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetWorkspaceSkillRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkspaceSkillRun>>(
            [
                new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-tests", ProjectName = "Bishop.App.Tests", RecordedAt = DateTimeOffset.UtcNow, GitSha = "abc" },
                new WorkspaceSkillRun { Id = Guid.NewGuid(), WorkspaceId = workspaceId, SkillName = "bish-tests", ProjectName = "Bishop.Core.Tests", RecordedAt = DateTimeOffset.UtcNow, GitSha = "def" },
            ]));

        await _vm.LoadAsync(workspaceId, @"C:\fake");

        var testsRows = _vm.Rows.Where(r => r.SkillName == "bish-tests").ToList();
        testsRows.Should().HaveCount(2);
        testsRows.Select(r => r.ProjectName).Should().BeEquivalentTo(new[] { "Bishop.App.Tests", "Bishop.Core.Tests" });
        testsRows.Select(r => r.DisplayLabel).Should().BeEquivalentTo(new[] { "bish-tests · Bishop.App.Tests", "bish-tests · Bishop.Core.Tests" });

        _vm.Rows.Where(r => r.SkillName != "bish-tests").Should().HaveCount(TrackedSkills.Length - 1);
    }

    [Fact]
    public async Task LoadAsync_ModelChangeDoesNotPersistAcrossRefresh()
    {
        await _vm.LoadAsync(Guid.NewGuid(), @"C:\fake");
        var row = _vm.Rows.First(r => r.SkillName == "bish-arch");
        row.SelectModelCommand.Execute("claude-haiku-4-5-20251001");

        await _vm.RefreshCommand.ExecuteAsync(null);

        var refreshedRow = _vm.Rows.First(r => r.SkillName == "bish-arch");
        refreshedRow.SelectedModelId.Should().Be("claude-sonnet-4-6");
    }
}
