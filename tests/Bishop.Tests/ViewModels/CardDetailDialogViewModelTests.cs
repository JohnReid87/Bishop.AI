using Bishop.App.Cards.UpdateCard;
using Bishop.App.Git;
using Bishop.App.Skills;
using Bishop.Core;
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

    [Fact]
    public void SetClaudeTotals_PopulatesClaudeTotalsText()
    {
        var vm = NewVm();
        vm.HasClaudeTotals.Should().BeFalse();

        vm.SetClaudeTotals(1000, 500, 3);

        vm.HasClaudeTotals.Should().BeTrue();
        vm.ClaudeTotalsText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SetCommit_WithUnpushedCommit_ShowsTextNotLink()
    {
        var vm = NewVm();
        vm.IsCommitVisible.Should().BeFalse();

        vm.SetCommit(new CommitInfo("abc1234", "abc1234def56789", "feat: Something", "", DateTimeOffset.UtcNow, IsPushed: false));

        vm.CommitShortHash.Should().Be("abc1234");
        vm.IsCommitVisible.Should().BeTrue();
        vm.IsCommitTextVisible.Should().BeTrue();
        vm.IsCommitLinkVisible.Should().BeFalse();
    }

    [Fact]
    public void SetCommit_WithPushedCommitAndGitHubRepo_ExposesLink()
    {
        var vm = NewVm(gitHubRepo: "owner/repo");

        vm.SetCommit(new CommitInfo("abc1234", "abc1234def56789", "feat: Something", "", DateTimeOffset.UtcNow, IsPushed: true));

        vm.CommitUrl.Should().Be("https://github.com/owner/repo/commit/abc1234def56789");
        vm.IsCommitLinkVisible.Should().BeTrue();
        vm.IsCommitTextVisible.Should().BeFalse();
    }

    [Fact]
    public void StartTitleEdit_TogglesTitleEditing()
    {
        var vm = NewVm();
        vm.IsTitleEditing.Should().BeFalse();

        vm.StartTitleEdit();
        vm.IsTitleEditing.Should().BeTrue();

        vm.CancelTitleEdit();
        vm.IsTitleEditing.Should().BeFalse();
    }

    [Fact]
    public void StartDescriptionEdit_TogglesDescriptionEditing()
    {
        var vm = NewVm();
        vm.IsDescriptionEditing.Should().BeFalse();

        vm.StartDescriptionEdit();
        vm.IsDescriptionEditing.Should().BeTrue();

        vm.CancelDescriptionEdit();
        vm.IsDescriptionEditing.Should().BeFalse();
    }

    [Fact]
    public async Task CommitTitleAsync_NoOpForBlankOrUnchangedTitle()
    {
        var mediator = Substitute.For<IMediator>();
        var vm = NewVm(mediator: mediator);

        vm.StartTitleEdit();
        await vm.CommitTitleAsync("   ");
        vm.IsTitleEditing.Should().BeFalse();

        vm.StartTitleEdit();
        await vm.CommitTitleAsync("T");
        vm.IsTitleEditing.Should().BeFalse();

        await mediator.DidNotReceive().Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
        vm.Updated.Should().BeFalse();
    }

    [Fact]
    public async Task CommitTitleAsync_UpdatesTitleOnSuccess()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), Title = "New Title", LaneName = "To Do" });
        var vm = NewVm(mediator: mediator);

        await vm.CommitTitleAsync("New Title");

        vm.Title.Should().Be("New Title");
        vm.Updated.Should().BeTrue();
        vm.EditError.Should().BeNull();
    }

    [Fact]
    public async Task CommitTitleAsync_RollsBackOnFailure()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(new Exception("DB error")));
        var vm = NewVm(mediator: mediator);

        await vm.CommitTitleAsync("New Title");

        vm.Title.Should().Be("T");
        vm.EditError.Should().Be("Failed to save title.");
    }

    [Fact]
    public async Task CommitDescriptionAsync_NoOpWhenUnchanged()
    {
        var mediator = Substitute.For<IMediator>();
        var vm = NewVm(mediator: mediator);

        await vm.CommitDescriptionAsync("D");

        await mediator.DidNotReceive().Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
        vm.Updated.Should().BeFalse();
    }

    [Fact]
    public async Task CommitDescriptionAsync_UpdatesDescriptionOnSuccess()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });
        var vm = NewVm(mediator: mediator);

        await vm.CommitDescriptionAsync("New description");

        vm.Description.Should().Be("New description");
        vm.Updated.Should().BeTrue();
    }

    [Fact]
    public async Task CommitDescriptionAsync_RollsBackOnFailure()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(new Exception("DB error")));
        var vm = NewVm(mediator: mediator);

        await vm.CommitDescriptionAsync("New description");

        vm.Description.Should().Be("D");
        vm.EditError.Should().Be("Failed to save description.");
    }

    [Fact]
    public async Task ClearTagAsync_ClearsTagOnSuccess()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });
        var card = new CardViewModel
        {
            Id = Guid.NewGuid(), Number = 1, Title = "T", Description = "D",
            LaneName = "To Do", TagName = "feature", TagColour = "#7fa87a",
        };
        var vm = NewVm(card: card, mediator: mediator);

        await vm.ClearTagAsync();

        vm.TagName.Should().BeNull();
        vm.TagColour.Should().BeNull();
        vm.Updated.Should().BeTrue();
        vm.EditError.Should().BeNull();
    }

    [Fact]
    public async Task ClearTagAsync_RollsBackOnFailure()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(new Exception("DB error")));
        var card = new CardViewModel
        {
            Id = Guid.NewGuid(), Number = 1, Title = "T", Description = "D",
            LaneName = "To Do", TagName = "feature", TagColour = "#7fa87a",
        };
        var vm = NewVm(card: card, mediator: mediator);

        await vm.ClearTagAsync();

        vm.TagName.Should().Be("feature");
        vm.TagColour.Should().Be("#7fa87a");
        vm.EditError.Should().Be("Failed to remove tag.");
    }

    [Fact]
    public async Task SetTagAsync_SetsTagOnSuccess()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });
        var vm = NewVm(mediator: mediator);

        await vm.SetTagAsync("bug", "#c97a8a");

        vm.TagName.Should().Be("bug");
        vm.TagColour.Should().Be("#c97a8a");
        vm.Updated.Should().BeTrue();
        vm.EditError.Should().BeNull();
    }

    [Fact]
    public async Task SetTagAsync_RollsBackOnFailure()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(new Exception("DB error")));
        var vm = NewVm(mediator: mediator);

        await vm.SetTagAsync("bug", "#c97a8a");

        vm.TagName.Should().BeNull();
        vm.EditError.Should().Be("Failed to add tag.");
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
