using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Git;
using Bishop.App.Skills;
using Bishop.App.Tags.ListTagsByWorkspace;
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
        vm.ClaudeTotalsText.Should().Be("Claude: 3 runs, 1.0k in / 500 out");
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
        var card = NewCard();
        var vm = NewVm(card: card, mediator: mediator);
        mediator.Send(
            Arg.Is<UpdateCardCommand>(c => c.CardId == vm.CardId && c.Title == "New Title"),
            Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), Title = "New Title", LaneName = "To Do" });

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
    public async Task CommitTitleAsync_TrimsWhitespaceBeforeSaving()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), Title = "New", LaneName = "To Do" });
        var vm = NewVm(mediator: mediator);

        await vm.CommitTitleAsync("  New  ");

        vm.Title.Should().Be("New");
        vm.Updated.Should().BeTrue();
        vm.EditError.Should().BeNull();
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
        var card = NewCard();
        var vm = NewVm(card: card, mediator: mediator);
        mediator.Send(
            Arg.Is<UpdateCardCommand>(c => c.CardId == vm.CardId && c.Description == "New description"),
            Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });

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
    public async Task CommitDescriptionAsync_SavesEmptyDescription()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });
        var vm = NewVm(mediator: mediator);

        await vm.CommitDescriptionAsync("");

        vm.Description.Should().Be("");
        vm.Updated.Should().BeTrue();
        vm.EditError.Should().BeNull();
    }

    [Fact]
    public async Task CommitDescriptionAsync_NoOpWhenDescriptionAlreadyEmpty()
    {
        var mediator = Substitute.For<IMediator>();
        var vm = NewVm(NewCard(description: ""), mediator: mediator);

        await vm.CommitDescriptionAsync("");

        await mediator.DidNotReceive().Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
        vm.Updated.Should().BeFalse();
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

    [Fact]
    public async Task SetTagAsync_RollsBackOnFailure_RestoresPriorNonNullTag()
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

        await vm.SetTagAsync("bug", "#c97a8a");

        vm.TagName.Should().Be("feature");
        vm.TagColour.Should().Be("#7fa87a");
        vm.EditError.Should().Be("Failed to add tag.");
    }

    [Fact]
    public async Task ToggleClosedAsync_ClosesCardWhenOpen()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<CloseCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });
        var vm = NewVm(mediator: mediator);

        await vm.ToggleClosedCommand.ExecuteAsync(null);

        vm.IsClosed.Should().BeTrue();
        vm.Updated.Should().BeTrue();
        vm.EditError.Should().BeNull();
        await mediator.Received(1).Send(
            Arg.Is<CloseCardCommand>(c => c.CardId == vm.CardId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleClosedAsync_ReopensCardWhenClosed()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ReopenCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });
        var vm = NewVm(mediator: mediator);
        vm.IsClosed = true;

        await vm.ToggleClosedCommand.ExecuteAsync(null);

        vm.IsClosed.Should().BeFalse();
        vm.Updated.Should().BeTrue();
        vm.EditError.Should().BeNull();
        await mediator.Received(1).Send(
            Arg.Is<ReopenCardCommand>(c => c.CardId == vm.CardId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleClosedAsync_SetsEditErrorOnFailure()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<CloseCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(new Exception("error")));
        var vm = NewVm(mediator: mediator);

        await vm.ToggleClosedCommand.ExecuteAsync(null);

        vm.IsClosed.Should().BeFalse();
        vm.Updated.Should().BeFalse();
        vm.EditError.Should().Be("Failed to update closed state.");
    }

    [Fact]
    public async Task PushToGitHubAsync_SetsGitHubIssueNumberOnSuccess()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do", GitHubIssueNumber = 42 });
        var vm = NewVm(mediator: mediator, gitHubRepo: "owner/repo");

        await vm.PushToGitHubCommand.ExecuteAsync(null);

        vm.GitHubIssueNumber.Should().Be(42);
        vm.Updated.Should().BeTrue();
        vm.PushError.Should().BeNull();
    }

    [Fact]
    public async Task PushToGitHubAsync_PopulatesPushErrorOnFailure()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(new Exception("Push failed")));
        var vm = NewVm(mediator: mediator, gitHubRepo: "owner/repo");

        await vm.PushToGitHubCommand.ExecuteAsync(null);

        vm.GitHubIssueNumber.Should().BeNull();
        vm.Updated.Should().BeFalse();
        vm.PushError.Should().Be("Push failed");
    }

    [Fact]
    public async Task ConfirmDeleteAsync_SetsDeletedAndSendsRemoveCommand()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RemoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);
        var vm = NewVm(mediator: mediator);

        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        vm.Deleted.Should().BeTrue();
        await mediator.Received(1).Send(
            Arg.Is<RemoveCardCommand>(c => c.CardId == vm.CardId),
            Arg.Any<CancellationToken>());
    }

    // ── LinkableDescription ───────────────────────────────────────────────────

    [Fact]
    public void LinkableDescription_BeforeLoadCardNumbers_ReturnsRawDescription()
    {
        var vm = NewVm(NewCard(description: "See #42 for details"));

        vm.LinkableDescription.Should().Be("See #42 for details");
    }

    [Fact]
    public async Task LinkableDescription_AfterLoadCardNumbers_LinksValidCardRefs()
    {
        var mediator = Substitute.For<IMediator>();
        IReadOnlyList<Card> cards = [new Card { Id = Guid.NewGuid(), Number = 42, LaneName = "To Do" }];
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>()).Returns(cards);
        var vm = NewVm(NewCard(description: "See #42 for details"), mediator: mediator);

        await vm.LoadCardNumbersAsync();

        vm.LinkableDescription.Should().Be("See [#42](bishop://card/42) for details");
    }

    [Fact]
    public async Task LinkableDescription_UnknownCardRef_RendersAsStrikethrough()
    {
        var mediator = Substitute.For<IMediator>();
        IReadOnlyList<Card> cards = [new Card { Id = Guid.NewGuid(), Number = 99, LaneName = "To Do" }];
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>()).Returns(cards);
        var vm = NewVm(NewCard(description: "See #42 for details"), mediator: mediator);

        await vm.LoadCardNumbersAsync();

        vm.LinkableDescription.Should().Be("See ~~#42~~ for details");
    }

    [Fact]
    public async Task LinkableDescription_EscapedRef_NotConverted()
    {
        var mediator = Substitute.For<IMediator>();
        IReadOnlyList<Card> cards = [new Card { Id = Guid.NewGuid(), Number = 42, LaneName = "To Do" }];
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>()).Returns(cards);
        var vm = NewVm(NewCard(description: @"\#42"), mediator: mediator);

        await vm.LoadCardNumbersAsync();

        vm.LinkableDescription.Should().Be(@"\#42");
    }

    [Fact]
    public async Task LinkableDescription_RefInsideCodeSpan_NotConverted()
    {
        var mediator = Substitute.For<IMediator>();
        IReadOnlyList<Card> cards = [new Card { Id = Guid.NewGuid(), Number = 42, LaneName = "To Do" }];
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>()).Returns(cards);
        var vm = NewVm(NewCard(description: "See `#42` for details"), mediator: mediator);

        await vm.LoadCardNumbersAsync();

        vm.LinkableDescription.Should().Be("See `#42` for details");
    }

    [Fact]
    public async Task LinkableDescription_RefInsideFencedCodeBlock_NotConverted()
    {
        var mediator = Substitute.For<IMediator>();
        IReadOnlyList<Card> cards = [new Card { Id = Guid.NewGuid(), Number = 42, LaneName = "To Do" }];
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>()).Returns(cards);
        var vm = NewVm(NewCard(description: "```\n#42\n```"), mediator: mediator);

        await vm.LoadCardNumbersAsync();

        vm.LinkableDescription.Should().Be("```\n#42\n```");
    }

    [Fact]
    public async Task LoadCardNumbersAsync_SwallowsExceptionAndReturnsRawDescription()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<Card>>(new Exception("DB error")));
        var vm = NewVm(NewCard(description: "See #42 for details"), mediator: mediator);

        await vm.LoadCardNumbersAsync();

        vm.LinkableDescription.Should().Be("See #42 for details");
    }

    // ── NavigateTo ────────────────────────────────────────────────────────────

    [Fact]
    public void NavigateTo_UpdatesAllCardProperties()
    {
        var vm = NewVm();
        vm.StartTitleEdit();
        vm.StartDescriptionEdit();
        vm.RequestDeleteCommand.Execute(null);
        vm.EditError = "old error";
        vm.PushError = "old push error";
        vm.SetCommit(new CommitInfo("abc", "abcdef", "msg", "", DateTimeOffset.UtcNow, IsPushed: false));
        vm.SetClaudeTotals(100, 50, 1);

        var newId = Guid.NewGuid();
        var target = new CardViewModel
        {
            Id = newId,
            Number = 77,
            Title = "New Title",
            Description = "New desc",
            LaneName = "Done",
            TagName = "bug",
            TagColour = "#ff0000",
            IsClosed = true,
            GitHubIssueNumber = 5,
        };

        vm.NavigateTo(target, canGoBack: true);

        vm.CardId.Should().Be(newId);
        vm.Number.Should().Be(77);
        vm.Title.Should().Be("New Title");
        vm.Description.Should().Be("New desc");
        vm.LaneName.Should().Be("Done");
        vm.TagName.Should().Be("bug");
        vm.TagColour.Should().Be("#ff0000");
        vm.IsClosed.Should().BeTrue();
        vm.GitHubIssueNumber.Should().Be(5);
        vm.IsTitleEditing.Should().BeFalse();
        vm.IsDescriptionEditing.Should().BeFalse();
        vm.ShowDeleteConfirm.Should().BeFalse();
        vm.EditError.Should().BeNull();
        vm.PushError.Should().BeNull();
        vm.CommitShortHash.Should().BeNull();
        vm.ClaudeTotalsText.Should().BeNull();
        vm.CanGoBack.Should().BeTrue();
    }

    [Fact]
    public void NavigateTo_CanGoBack_False_ClearsBack()
    {
        var vm = NewVm();
        vm.NavigateTo(NewCard(), canGoBack: false);

        vm.CanGoBack.Should().BeFalse();
    }

    // ── GetWorkspaceTagsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetWorkspaceTagsAsync_DelegatesToMediatorWithWorkspaceId()
    {
        var mediator = Substitute.For<IMediator>();
        var workspaceId = Guid.NewGuid();
        IReadOnlyList<TagInfo> tags = [];
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(tags);
        var vm = new CardDetailDialogViewModel(NewCard(), [], workspaceId, null, mediator);

        var result = await vm.GetWorkspaceTagsAsync();

        result.Should().BeSameAs(tags);
        await mediator.Received(1).Send(
            Arg.Is<ListTagsByWorkspaceQuery>(q => q.WorkspaceId == workspaceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetWorkspaceTagsAsync_MediatorThrows_ReturnsEmptyWithoutPropagating()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<TagInfo>>(new InvalidOperationException("DB error")));
        var vm = new CardDetailDialogViewModel(NewCard(), [], Guid.NewGuid(), null, mediator);

        var result = await vm.GetWorkspaceTagsAsync();

        result.Should().BeEmpty();
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
