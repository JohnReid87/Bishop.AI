using Bishop.App.Findings.GetFindingsBySkillAndProject;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Findings;
using Bishop.ViewModels.Skills;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Findings;

public class FindingsViewModelTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private const string WorkspacePath = @"C:\fake\workspace";
    private const string SkillName = "bish-arch";

    private static FindingRecord MakeRecord(
        string status = "pending",
        string title = "A finding",
        string body = "body text",
        string? file = null,
        string? symbol = null,
        string? severity = null) =>
        new(
            Id: Guid.NewGuid(),
            Title: title,
            Body: body,
            Severity: severity,
            File: file,
            Symbol: symbol,
            Rule: null,
            Status: status,
            RebuttalText: null,
            LinkedCardId: null);

    private static FindingsViewModel MakeVm(ISender? mediator = null) =>
        new(
            mediator ?? Substitute.For<ISender>(),
            Substitute.For<ICardDetailDialogService>(),
            Substitute.For<ISkillTagMap>());

    // --- Header ---

    [Fact]
    public void Header_WithoutProjectName_ReturnsSkillNameOnly()
    {
        var vm = MakeVm();

        vm.SkillName.Should().BeEmpty();
        vm.Header.Should().Be(" — findings");
    }

    [Fact]
    public async Task Header_AfterLoad_WithoutProject_ShowsSkillName()
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetFindingsBySkillAndProjectQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FindingRecord>());

        var vm = MakeVm(mediator);

        await vm.LoadAsync(WorkspaceId, WorkspacePath, null, SkillName, null);

        vm.Header.Should().Be("bish-arch — findings");
    }

    [Fact]
    public async Task Header_AfterLoad_WithProject_ShowsSkillAndProject()
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetFindingsBySkillAndProjectQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FindingRecord>());

        var vm = MakeVm(mediator);

        await vm.LoadAsync(WorkspaceId, WorkspacePath, null, SkillName, "Bishop.App");

        vm.Header.Should().Be("bish-arch · Bishop.App — findings");
    }

    // --- IsEmpty / HasResolved ---

    [Fact]
    public void IsEmpty_TrueOnConstruction()
    {
        MakeVm().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmpty_FalseAfterLoadWithFindings()
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetFindingsBySkillAndProjectQuery>(), Arg.Any<CancellationToken>())
            .Returns(new[] { MakeRecord() });

        var vm = MakeVm(mediator);
        await vm.LoadAsync(WorkspaceId, WorkspacePath, null, SkillName, null);

        vm.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void HasResolved_FalseOnConstruction()
    {
        MakeVm().HasResolved.Should().BeFalse();
    }

    [Fact]
    public async Task HasResolved_TrueWhenResolvedFindingPresent()
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetFindingsBySkillAndProjectQuery>(), Arg.Any<CancellationToken>())
            .Returns(new[] { MakeRecord(status: "resolved") });

        var vm = MakeVm(mediator);
        await vm.LoadAsync(WorkspaceId, WorkspacePath, null, SkillName, null);

        vm.HasResolved.Should().BeTrue();
        vm.Findings.Should().BeEmpty();
        vm.ResolvedFindings.Should().HaveCount(1);
    }

    // --- LoadAsync ---

    [Fact]
    public async Task LoadAsync_SetsSkillNameAndProjectName()
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetFindingsBySkillAndProjectQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FindingRecord>());

        var vm = MakeVm(mediator);
        await vm.LoadAsync(WorkspaceId, WorkspacePath, "my-repo", SkillName, "Bishop.App");

        vm.SkillName.Should().Be(SkillName);
        vm.ProjectName.Should().Be("Bishop.App");
    }

    [Fact]
    public async Task LoadAsync_SeparatesResolvedFromActive()
    {
        var mediator = Substitute.For<ISender>();
        var records = new[]
        {
            MakeRecord(status: "pending"),
            MakeRecord(status: "dismissed"),
            MakeRecord(status: "resolved"),
            MakeRecord(status: "resolved"),
        };
        mediator.Send(Arg.Any<GetFindingsBySkillAndProjectQuery>(), Arg.Any<CancellationToken>())
            .Returns(records);

        var vm = MakeVm(mediator);
        await vm.LoadAsync(WorkspaceId, WorkspacePath, null, SkillName, null);

        vm.Findings.Should().HaveCount(2);
        vm.ResolvedFindings.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_ClearsExistingFindingsBeforeReload()
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetFindingsBySkillAndProjectQuery>(), Arg.Any<CancellationToken>())
            .Returns(new[] { MakeRecord() }, Array.Empty<FindingRecord>());

        var vm = MakeVm(mediator);
        await vm.LoadAsync(WorkspaceId, WorkspacePath, null, SkillName, null);
        vm.Findings.Should().HaveCount(1);

        await vm.LoadAsync(WorkspaceId, WorkspacePath, null, SkillName, null);
        vm.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_SendsCorrectQuery()
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetFindingsBySkillAndProjectQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FindingRecord>());

        var vm = MakeVm(mediator);
        await vm.LoadAsync(WorkspaceId, WorkspacePath, null, SkillName, "Bishop.App");

        await mediator.Received(1).Send(
            Arg.Is<GetFindingsBySkillAndProjectQuery>(q =>
                q.WorkspaceId == WorkspaceId &&
                q.SkillName == SkillName &&
                q.ProjectName == "Bishop.App"),
            Arg.Any<CancellationToken>());
    }

    // --- Matches ---

    [Fact]
    public void Matches_EmptyFilterText_ReturnsTrue()
    {
        var vm = MakeVm();
        var item = MakeVm().Findings.Count == 0 ? null : vm.Findings[0];
        var record = MakeRecord(title: "Foo");
        var finding = new FindingItemViewModel(
            record, SkillName, WorkspaceId, WorkspacePath, null,
            Substitute.For<ISender>(), Substitute.For<ICardDetailDialogService>(), Substitute.For<ISkillTagMap>());

        vm.FilterText = string.Empty;
        vm.Matches(finding).Should().BeTrue();
    }

    [Theory]
    [InlineData("unused variable", "unused variable", null, null, null, true)]
    [InlineData("UNUSED", "unused variable", null, null, null, true)]
    [InlineData("src/Foo.cs", "some title", null, "src/Foo.cs", null, true)]
    [InlineData("MyMethod", "some title", null, null, "MyMethod", true)]
    [InlineData("high", "some title", null, null, null, false)]
    [InlineData("notpresent", "some title", null, null, null, false)]
    public void Matches_FilterByFields(
        string filterText,
        string title,
        string? severity,
        string? file,
        string? symbol,
        bool expected)
    {
        var vm = MakeVm();
        vm.FilterText = filterText;

        var record = MakeRecord(title: title, severity: severity, file: file, symbol: symbol);
        var finding = new FindingItemViewModel(
            record, SkillName, WorkspaceId, WorkspacePath, null,
            Substitute.For<ISender>(), Substitute.For<ICardDetailDialogService>(), Substitute.For<ISkillTagMap>());

        vm.Matches(finding).Should().Be(expected);
    }

    [Fact]
    public void Matches_FilterBySeverity_ReturnsTrue()
    {
        var vm = MakeVm();
        vm.FilterText = "high";

        var record = MakeRecord(severity: "high");
        var finding = new FindingItemViewModel(
            record, SkillName, WorkspaceId, WorkspacePath, null,
            Substitute.For<ISender>(), Substitute.For<ICardDetailDialogService>(), Substitute.For<ISkillTagMap>());

        vm.Matches(finding).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhitespaceOnlyFilterText_ReturnsTrue()
    {
        var vm = MakeVm();
        vm.FilterText = "   ";

        var record = MakeRecord();
        var finding = new FindingItemViewModel(
            record, SkillName, WorkspaceId, WorkspacePath, null,
            Substitute.For<ISender>(), Substitute.For<ICardDetailDialogService>(), Substitute.For<ISkillTagMap>());

        vm.Matches(finding).Should().BeTrue();
    }
}
