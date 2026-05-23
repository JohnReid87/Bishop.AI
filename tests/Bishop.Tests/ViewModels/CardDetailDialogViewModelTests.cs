using Bishop.App.Skills;
using Bishop.ViewModels;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class CardDetailDialogViewModelTests
{
    [Fact]
    public void Constructor_HydratesFromCardViewModel()
    {
        var card = new CardViewModel
        {
            Id = Guid.NewGuid(),
            Number = 42,
            Title = "T",
            Description = "D",
            LaneName = "Doing",
            IsClosed = false,
            GitHubIssueNumber = null,
            TagName = "feature",
            TagColour = "#22ff44",
        };

        var vm = NewVm(card);

        vm.CardId.Should().Be(card.Id);
        vm.Number.Should().Be(42);
        vm.NumberDisplay.Should().Be("#42");
        vm.Title.Should().Be("T");
        vm.Description.Should().Be("D");
        vm.LaneName.Should().Be("Doing");
        vm.TagName.Should().Be("feature");
        vm.TagColour.Should().Be("#22ff44");
        vm.IsTagVisible.Should().BeTrue();
        vm.IsAddTagButtonVisible.Should().BeFalse();
    }

    [Fact]
    public void IsSkillsButtonVisible_TrueOnlyWhenAnySkillsProvided()
    {
        NewVm(cardSkills: []).IsSkillsButtonVisible.Should().BeFalse();
        NewVm(cardSkills: [Stub.Skill()]).IsSkillsButtonVisible.Should().BeTrue();
    }

    [Fact]
    public void IsPushSectionVisible_ReflectsRepoLink()
    {
        NewVm(gitHubRepo: null).IsPushSectionVisible.Should().BeFalse();
        NewVm(gitHubRepo: "owner/repo").IsPushSectionVisible.Should().BeTrue();
    }

    [Fact]
    public void CanPushToGitHub_RequiresLinkAndNoExistingIssue()
    {
        var card = NewCard(gitHubIssueNumber: null);

        var withRepo = NewVm(card, gitHubRepo: "owner/repo");
        withRepo.CanPushToGitHub.Should().BeTrue();
        withRepo.IsPushButtonVisible.Should().BeTrue();
        withRepo.IsGitHubLinkVisible.Should().BeFalse();

        withRepo.GitHubIssueNumber = 7;
        withRepo.CanPushToGitHub.Should().BeFalse();
        withRepo.IsPushButtonVisible.Should().BeFalse();
        withRepo.IsGitHubLinkVisible.Should().BeTrue();
        withRepo.GitHubIssueUrl.Should().Be("https://github.com/owner/repo/issues/7");
    }

    [Fact]
    public void HasDescription_TogglesOnDescriptionSet()
    {
        var vm = NewVm(NewCard(description: ""));

        vm.HasDescription.Should().BeFalse();

        vm.Description = "Some text";

        vm.HasDescription.Should().BeTrue();
    }

    [Fact]
    public void CloseReopenText_FlipsWithIsClosed()
    {
        var vm = NewVm();

        vm.IsClosed = false;
        vm.CloseReopenText.Should().Be("Close");

        vm.IsClosed = true;
        vm.CloseReopenText.Should().Be("Reopen");
    }

    [Fact]
    public void RequestDelete_ShowsDeleteConfirm()
    {
        var vm = NewVm();
        vm.ShowDeleteConfirm.Should().BeFalse();

        vm.RequestDeleteCommand.Execute(null);

        vm.ShowDeleteConfirm.Should().BeTrue();

        vm.CancelDeleteCommand.Execute(null);
        vm.ShowDeleteConfirm.Should().BeFalse();
    }

    private static CardViewModel NewCard(string? description = "D", int? gitHubIssueNumber = null) => new()
    {
        Id = Guid.NewGuid(),
        Number = 1,
        Title = "T",
        Description = description ?? string.Empty,
        LaneName = "To Do",
        GitHubIssueNumber = gitHubIssueNumber,
    };

    private static CardDetailDialogViewModel NewVm(
        CardViewModel? card = null,
        SkillMenuItem[]? cardSkills = null,
        string? gitHubRepo = null,
        IMediator? mediator = null) =>
        new(card ?? NewCard(),
            cardSkills ?? [],
            workspaceId: Guid.NewGuid(),
            gitHubRepo: gitHubRepo,
            mediator: mediator ?? Substitute.For<IMediator>());

    private static class Stub
    {
        public static SkillMenuItem Skill() => new(
            Name: "test",
            Skill: new Bishop.Core.Skills.InstalledSkill(
                Name: "test",
                Description: "desc",
                Scope: ["card"],
                Command: "/test"),
            HasSeparatorAfter: false);
    }
}
