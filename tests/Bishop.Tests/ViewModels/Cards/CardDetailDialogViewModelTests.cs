using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Git;
using Bishop.App.Skills;
using Bishop.App.Tags.ListTags;
using Bishop.Core;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Cards;

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
    public void StartTitleEdit_SetsTitleEditingTrue()
    {
        var vm = NewVm();

        vm.StartTitleEdit();

        vm.IsTitleEditing.Should().BeTrue();
    }

    [Fact]
    public void CancelTitleEdit_SetsTitleEditingFalseAndPreservesTitle()
    {
        var vm = NewVm();
        vm.StartTitleEdit();

        vm.CancelTitleEdit();

        vm.IsTitleEditing.Should().BeFalse();
        vm.Title.Should().Be("T");
    }

    [Fact]
    public void StartDescriptionEdit_SetsDescriptionEditingTrue()
    {
        var vm = NewVm();

        vm.StartDescriptionEdit();

        vm.IsDescriptionEditing.Should().BeTrue();
    }

    [Fact]
    public void CancelDescriptionEdit_SetsDescriptionEditingFalseAndPreservesDescription()
    {
        var vm = NewVm();
        vm.StartDescriptionEdit();

        vm.CancelDescriptionEdit();

        vm.IsDescriptionEditing.Should().BeFalse();
        vm.Description.Should().Be("D");
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
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), Title = "New Title", LaneName = "To Do" });

        await vm.CommitTitleAsync("New Title");

        vm.Title.Should().Be("New Title");
        vm.Updated.Should().BeTrue();
        vm.EditError.Should().BeNull();
        await mediator.Received(1).Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitTitleAsync_RollsBackOnFailure()
    {
        var mediator = Substitute.For<IMediator>();
        var exception = new Exception("DB error");
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(exception));
        var vm = NewVm(mediator: mediator);

        await vm.CommitTitleAsync("New Title");

        vm.Title.Should().Be("T");
        vm.EditError.Should().Be(exception.Message);
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
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });

        await vm.CommitDescriptionAsync("New description");

        vm.Description.Should().Be("New description");
        vm.Updated.Should().BeTrue();
        vm.EditError.Should().BeNull();
        await mediator.Received(1).Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitDescriptionAsync_RollsBackOnFailure()
    {
        var mediator = Substitute.For<IMediator>();
        var exception = new Exception("DB error");
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(exception));
        var vm = NewVm(mediator: mediator);

        await vm.CommitDescriptionAsync("New description");

        vm.Description.Should().Be("D");
        vm.EditError.Should().Be(exception.Message);
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
        var card = new CardViewModel
        {
            Id = Guid.NewGuid(), Number = 1, Title = "T", Description = "D",
            LaneName = "To Do", TagName = "feature", TagColour = "#7fa87a",
        };
        var vm = NewVm(card: card, mediator: mediator);
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });

        await vm.ClearTagAsync();

        vm.TagName.Should().BeNull();
        vm.TagColour.Should().BeNull();
        vm.Updated.Should().BeTrue();
        vm.EditError.Should().BeNull();
        await mediator.Received(1).Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
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
        var card = NewCard();
        var vm = NewVm(card: card, mediator: mediator);
        mediator.Send(
            Arg.Is<CloseCardCommand>(c => c.CardId == vm.CardId),
            Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });

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
        var card = NewCard();
        var vm = NewVm(card: card, mediator: mediator);
        vm.IsClosed = true;
        mediator.Send(
            Arg.Is<ReopenCardCommand>(c => c.CardId == vm.CardId),
            Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do" });

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
    public async Task PushToGitHubAsync_ClearsPushErrorBeforeRetry()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(new Exception("Push failed")));
        var vm = NewVm(mediator: mediator, gitHubRepo: "owner/repo");
        await vm.PushToGitHubCommand.ExecuteAsync(null);
        vm.PushError.Should().Be("Push failed");

        mediator.Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), LaneName = "To Do", GitHubIssueNumber = 5 });

        await vm.PushToGitHubCommand.ExecuteAsync(null);

        vm.PushError.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmDeleteAsync_SetsDeletedAndSendsRemoveCommand()
    {
        var mediator = Substitute.For<IMediator>();
        var vm = NewVm(mediator: mediator);

        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        vm.Deleted.Should().BeTrue();
        await mediator.Received(1).Send(
            Arg.Is<RemoveCardCommand>(c => c.CardId == vm.CardId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmDeleteAsync_MediatorThrows_PropagatesException()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RemoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Unit>(new Exception("Delete failed")));
        var vm = NewVm(mediator: mediator);

        Func<Task> act = () => vm.ConfirmDeleteCommand.ExecuteAsync(null);

        await act.Should().ThrowAsync<Exception>();
        vm.Deleted.Should().BeFalse();
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
    public async Task LinkableDescription_RefInsideTildeFencedCodeBlock_NotConverted()
    {
        var mediator = Substitute.For<IMediator>();
        IReadOnlyList<Card> cards = [new Card { Id = Guid.NewGuid(), Number = 42, LaneName = "To Do" }];
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>()).Returns(cards);
        var vm = NewVm(NewCard(description: "~~~\n#42\n~~~"), mediator: mediator);

        await vm.LoadCardNumbersAsync();

        vm.LinkableDescription.Should().Be("~~~\n#42\n~~~");
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

    [Fact]
    public async Task LoadCardNumbersAsync_SendsQueryWithCorrectWorkspaceId_PopulatesNumbers_RaisesPropertyChanged()
    {
        var mediator = Substitute.For<IMediator>();
        var workspaceId = Guid.NewGuid();
        IReadOnlyList<Card> cards = [new Card { Id = Guid.NewGuid(), Number = 7, LaneName = "To Do" }];
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>()).Returns(cards);
        var vm = new CardDetailDialogViewModel(NewCard(description: "#7"), [], workspaceId, null, mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), string.Empty, NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        await vm.LoadCardNumbersAsync();

        await mediator.Received(1).Send(
            Arg.Is<ListCardsByWorkspaceQuery>(q => q.WorkspaceId == workspaceId),
            Arg.Any<CancellationToken>());
        vm.LinkableDescription.Should().Be("[#7](bishop://card/7)");
        changedProperties.Should().Contain(nameof(vm.LinkableDescription));
    }

    [Fact]
    public async Task LinkableDescription_MultipleCardRefsInOneDescription_LinksAllValid()
    {
        var mediator = Substitute.For<IMediator>();
        IReadOnlyList<Card> cards =
        [
            new Card { Id = Guid.NewGuid(), Number = 1, LaneName = "To Do" },
            new Card { Id = Guid.NewGuid(), Number = 2, LaneName = "To Do" },
        ];
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>()).Returns(cards);
        var vm = NewVm(NewCard(description: "See #1 and #2"), mediator: mediator);

        await vm.LoadCardNumbersAsync();

        vm.LinkableDescription.Should().Be("See [#1](bishop://card/1) and [#2](bishop://card/2)");
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
    public async Task GetWorkspaceTagsAsync_DelegatesToMediator()
    {
        var mediator = Substitute.For<IMediator>();
        var workspaceId = Guid.NewGuid();
        IReadOnlyList<TagInfo> tags = [];
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(tags);
        var vm = new CardDetailDialogViewModel(NewCard(), [], workspaceId, null, mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), string.Empty, NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());

        var result = await vm.GetWorkspaceTagsAsync();

        result.Should().BeSameAs(tags);
        await mediator.Received(1).Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetWorkspaceTagsAsync_MediatorThrows_ReturnsEmptyWithoutPropagating()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<TagInfo>>(new InvalidOperationException("DB error")));
        var vm = new CardDetailDialogViewModel(NewCard(), [], Guid.NewGuid(), null, mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), string.Empty, NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());

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
            mediator: mediator ?? Substitute.For<IMediator>(),
            appSettings: Substitute.For<Bishop.App.Services.Settings.IAppSettings>(),
            workspacePath: string.Empty,
            logger: NullLogger<CardDetailDialogViewModel>.Instance,
            errorBus: Substitute.For<IErrorBus>());

    private static class Stub
    {
        public static SkillMenuItem Skill() => new(
            Name: "test",
            Skill: new Bishop.Core.Skills.InstalledSkill(
                Name: "test",
                Description: "desc",
                Scope: ["card"],
                Command: "/test"));
    }

    // ── New methods ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadExtrasAsync_SendsGetCardQueryAndGetCardCommitQuery()
    {
        var mediator = Substitute.For<IMediator>();
        var cardId = Guid.NewGuid();
        var card = new CardViewModel { Id = cardId, Title = "T", Description = "", LaneName = "To Do" };
        var vm = new CardDetailDialogViewModel(
            card, [], Guid.NewGuid(), null, mediator,
            Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), @"C:\repo",
            NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());
        var domainCard = new Bishop.Core.Card { Id = cardId, TotalInputTokens = 100, TotalOutputTokens = 50 };
        mediator.Send(Arg.Any<Bishop.App.Cards.GetCard.GetCardQuery>(), Arg.Any<CancellationToken>())
            .Returns(domainCard);
        mediator.Send(Arg.Any<Bishop.App.Git.GetCardCommit.GetCardCommitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Bishop.App.Git.GetCardCommit.GetCardCommitResult.NotFound());

        await vm.LoadExtrasAsync();

        await mediator.Received(1).Send(Arg.Any<Bishop.App.Cards.GetCard.GetCardQuery>(), Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(Arg.Any<Bishop.App.Git.GetCardCommit.GetCardCommitQuery>(), Arg.Any<CancellationToken>());
        vm.HasClaudeTotals.Should().BeTrue();
    }

    [Fact]
    public async Task GetCardByNumberAsync_ReturnsCardViewModel_WhenFound()
    {
        var mediator = Substitute.For<IMediator>();
        var workspaceId = Guid.NewGuid();
        var vm = new CardDetailDialogViewModel(
            NewCard(), [], workspaceId, null, mediator,
            Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), string.Empty,
            NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());
        var domainCard = new Bishop.Core.Card
        {
            Id = Guid.NewGuid(), Number = 7, Title = "Found", Description = "", LaneName = "To Do"
        };
        mediator.Send(Arg.Any<Bishop.App.Cards.GetCardByNumber.GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(domainCard);
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Bishop.Core.TagInfo>)[]);

        var result = await vm.GetCardByNumberAsync(7, isSkillsButtonVisible: true);

        result.Should().NotBeNull();
        result!.Number.Should().Be(7);
        result.IsSkillsButtonVisible.Should().BeTrue();
    }

    [Fact]
    public async Task GetCardByNumberAsync_ReturnsNull_WhenNotFound()
    {
        var mediator = Substitute.For<IMediator>();
        var vm = new CardDetailDialogViewModel(
            NewCard(), [], Guid.NewGuid(), null, mediator,
            Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), string.Empty,
            NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());
        mediator.Send(Arg.Any<Bishop.App.Cards.GetCardByNumber.GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns((Bishop.Core.Card?)null);

        var result = await vm.GetCardByNumberAsync(999, isSkillsButtonVisible: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task BuildSkillLaunchItemsAsync_RendersCardContextIntoCommand()
    {
        var menuItem = new Bishop.App.Skills.SkillMenuItem(
            Name: "bish-work-on-card",
            Skill: new Bishop.Core.Skills.InstalledSkill(
                Name: "bish-work-on-card",
                Description: "",
                Scope: ["card"],
                Command: "/bish-work-on-card {{card_number}}"));

        var card = new CardViewModel { Id = Guid.NewGuid(), Number = 42, Title = "T", Description = "D", LaneName = "Doing" };
        var vm = new CardDetailDialogViewModel(
            card, [menuItem], Guid.NewGuid(), null, Substitute.For<IMediator>(),
            Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), @"C:\repo",
            NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());

        var items = await vm.BuildSkillLaunchItemsAsync();

        items.Should().ContainSingle()
            .Which.RenderedCommand.Should().Be("/bish-work-on-card 42");
    }

    [Fact]
    public async Task BuildSkillLaunchItemsAsync_PropagatesSavedModelFromAppSettings()
    {
        var menuItem = new Bishop.App.Skills.SkillMenuItem(
            Name: "bish-arch",
            Skill: new Bishop.Core.Skills.InstalledSkill(
                Name: "bish-arch", Description: "", Scope: ["card"], Command: "/bish-arch"));

        var appSettings = Substitute.For<Bishop.App.Services.Settings.IAppSettings>();
        appSettings.GetAsync("skill.bish-arch.last_model", Arg.Any<CancellationToken>())
            .Returns("claude-opus-4-7");
        var vm = new CardDetailDialogViewModel(
            NewCard(), [menuItem], Guid.NewGuid(), null, Substitute.For<IMediator>(), appSettings,
            string.Empty, NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());

        var items = await vm.BuildSkillLaunchItemsAsync();

        items.Single().SavedModelId.Should().Be("claude-opus-4-7");
    }

    [Fact]
    public async Task BuildSkillLaunchItemsAsync_NeverRequiresStageForCardContext()
    {
        var menuItem = new Bishop.App.Skills.SkillMenuItem(
            Name: "bish-write-skill",
            Skill: new Bishop.Core.Skills.InstalledSkill(
                Name: "bish-write-skill", Description: "", Scope: ["card"], Command: "/bish-write-skill",
                Stage: true, StagePrompt: "Name?"));

        var vm = new CardDetailDialogViewModel(
            NewCard(), [menuItem], Guid.NewGuid(), null, Substitute.For<IMediator>(),
            Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), @"C:\repo",
            NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());

        var items = await vm.BuildSkillLaunchItemsAsync();

        items.Single().RequiresStage.Should().BeFalse();
    }

    [Fact]
    public async Task LaunchAsync_SendsLaunchSkillCommandWithRenderedCommand()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<Bishop.App.Skills.LaunchSkill.LaunchSkillCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var vm = new CardDetailDialogViewModel(
            NewCard(), [], Guid.NewGuid(), null, mediator,
            Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), @"C:\repo",
            NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());

        var item = new SkillLaunchItem("bish-work-on-card", null, "claude-sonnet-4-6",
            RenderedCommand: "/bish-work-on-card 42", RequiresStage: false, StagePrompt: null, StagePrefill: null, MarkdownBody: "");

        await vm.LaunchAsync(item, stagedText: null, new Bishop.App.Services.Terminal.TerminalSnap(), "claude-sonnet-4-6");

        await mediator.Received(1).Send(
            Arg.Is<Bishop.App.Skills.LaunchSkill.LaunchSkillCommand>(c =>
                c.WorkspacePath == @"C:\repo" &&
                c.RenderedCommand == "/bish-work-on-card 42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetSkillModelAsync_WritesToAppSettings()
    {
        var mediator = Substitute.For<IMediator>();
        var appSettings = Substitute.For<Bishop.App.Services.Settings.IAppSettings>();
        var vm = new CardDetailDialogViewModel(NewCard(), [], Guid.NewGuid(), null, mediator, appSettings, string.Empty, NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());

        await vm.SetSkillModelAsync("bish-arch", "claude-opus-4-7");

        await appSettings.Received(1).SetAsync("skill.bish-arch.last_model", "claude-opus-4-7", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitDescriptionAsync_NullDraft_TreatedAsEmpty()
    {
        var mediator = Substitute.For<IMediator>();
        var vm = NewVm(NewCard(description: ""), mediator: mediator);

        await vm.CommitDescriptionAsync(null!);

        await mediator.DidNotReceive().Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
        vm.Updated.Should().BeFalse();
    }

    [Fact]
    public async Task LoadExtrasAsync_GetCardQueryThrows_ReportsToErrorBusAndDoesNotPropagate()
    {
        var mediator = Substitute.For<IMediator>();
        var errorBus = Substitute.For<IErrorBus>();
        var exception = new InvalidOperationException("DB error");
        mediator.Send(Arg.Any<Bishop.App.Cards.GetCard.GetCardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Bishop.Core.Card?>(exception));
        var vm = new CardDetailDialogViewModel(
            NewCard(), [], Guid.NewGuid(), null, mediator,
            Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), @"C:\repo",
            NullLogger<CardDetailDialogViewModel>.Instance, errorBus);

        await vm.LoadExtrasAsync();

        errorBus.Received(1).Report(exception);
    }

    [Fact]
    public async Task LoadExtrasAsync_NullCard_SkipsCommitQuery()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<Bishop.App.Cards.GetCard.GetCardQuery>(), Arg.Any<CancellationToken>())
            .Returns((Bishop.Core.Card?)null);
        var vm = new CardDetailDialogViewModel(
            NewCard(), [], Guid.NewGuid(), null, mediator,
            Substitute.For<Bishop.App.Services.Settings.IAppSettings>(), @"C:\repo",
            NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());

        await vm.LoadExtrasAsync();

        await mediator.DidNotReceive().Send(Arg.Any<Bishop.App.Git.GetCardCommit.GetCardCommitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildSkillLaunchItemsAsync_NullSavedModel_FallsBackToDefault()
    {
        var menuItem = new Bishop.App.Skills.SkillMenuItem(
            Name: "bish-arch",
            Skill: new Bishop.Core.Skills.InstalledSkill(
                Name: "bish-arch", Description: "", Scope: ["card"], Command: "/bish-arch"));
        var appSettings = Substitute.For<Bishop.App.Services.Settings.IAppSettings>();
        appSettings.GetAsync("skill.bish-arch.last_model", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        var vm = new CardDetailDialogViewModel(
            NewCard(), [menuItem], Guid.NewGuid(), null, Substitute.For<IMediator>(), appSettings,
            string.Empty, NullLogger<CardDetailDialogViewModel>.Instance, Substitute.For<IErrorBus>());

        var items = await vm.BuildSkillLaunchItemsAsync();

        items.Single().SavedModelId.Should().Be(Bishop.App.Skills.SkillModelOptions.DefaultModelId);
    }

    [Fact]
    public void NumberDisplay_FormatsWithHash()
    {
        var card = new CardViewModel
        {
            Id = Guid.NewGuid(), Number = 42, Title = "T", Description = "", LaneName = "To Do",
        };
        var vm = NewVm(card);

        vm.NumberDisplay.Should().Be("#42");
    }
}
