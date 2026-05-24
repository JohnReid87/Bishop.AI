using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.Core;
using Bishop.ViewModels;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class WorkspaceBoardViewModelTests
{
    [Fact]
    public void IsSearchEmpty_TrueWhenSearchTextIsEmpty()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>());

        vm.IsSearchEmpty.Should().BeTrue();

        vm.SearchText = "lane";
        vm.IsSearchEmpty.Should().BeFalse();

        vm.SearchText = string.Empty;
        vm.IsSearchEmpty.Should().BeTrue();
    }

    [Fact]
    public void SearchText_RaisesIsSearchEmptyChanged()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>());
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SearchText = "x";

        changed.Should().Contain(nameof(WorkspaceBoardViewModel.IsSearchEmpty));
    }

    [Fact]
    public void SearchText_PropagatesFilterToLanes()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        lane.Cards.Add(new CardViewModel { Title = "Alpha" });
        lane.Cards.Add(new CardViewModel { Title = "Beta" });
        vm.Lanes.Add(lane);

        vm.SearchText = "alpha";

        lane.FilteredCards.Should().HaveCount(1);
        lane.FilteredCards[0].Title.Should().Be("Alpha");
    }

    [Fact]
    public async Task LoadAsync_BuildsLanesAndCardsFromMediator()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1), new("Doing", 2) });
        var card = new Card { Id = Guid.NewGuid(), Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" };
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        vm.Lanes.Should().HaveCount(2);
        vm.Lanes[0].Name.Should().Be("To Do");
        vm.Lanes[0].Cards.Should().HaveCount(1);
        vm.Lanes[1].Name.Should().Be("Doing");
        vm.Lanes[1].Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_AppliesExistingSearchFilterToNewLanes()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        var cardAlpha = new Card { Id = Guid.NewGuid(), Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" };
        var cardBeta = new Card { Id = Guid.NewGuid(), Number = 2, Title = "Beta", LaneName = "To Do", Description = "" };
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { cardAlpha, cardBeta });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        vm.SearchText = "alpha";

        await vm.LoadAsync(workspaceId);

        vm.Lanes[0].FilteredCards.Should().HaveCount(1);
        vm.Lanes[0].FilteredCards[0].Title.Should().Be("Alpha");
    }

    [Fact]
    public async Task LoadAsync_WithTaggedCard_SetsTagColourOnCardViewModel()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        var card = new Card { Id = Guid.NewGuid(), Number = 1, Title = "T", LaneName = "To Do", Description = "", TagName = "feature" };
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("feature", "#7fa87a") });

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        var cardVm = vm.Lanes[0].Cards[0];
        cardVm.TagName.Should().Be("feature");
        cardVm.TagColour.Should().Be("#7fa87a");
    }

    [Fact]
    public async Task RefreshCommand_WhenLanesUnchanged_UpdatesChangedCard()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" } });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha Updated", LaneName = "To Do", Description = "" } });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards.Should().HaveCount(1);
        vm.Lanes[0].Cards[0].Title.Should().Be("Alpha Updated");
    }

    [Fact]
    public async Task RefreshCommand_WhenLanesUnchanged_MatchingCardIsNotReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var card = new Card { Id = Guid.NewGuid(), Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().BeSameAs(originalCardVm);
    }

    [Fact]
    public async Task RefreshCommand_WhenLanesUnchanged_RemovesExtraCards()
    {
        var workspaceId = Guid.NewGuid();
        var card1 = new Card { Id = Guid.NewGuid(), Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" };
        var card2 = new Card { Id = Guid.NewGuid(), Number = 2, Title = "Beta", LaneName = "To Do", Description = "" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card1, card2 });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card1 });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards.Should().HaveCount(1);
        vm.Lanes[0].Cards[0].Title.Should().Be("Alpha");
    }

    [Fact]
    public async Task RefreshCommand_WhenLanesUnchanged_AddsNewCard()
    {
        var workspaceId = Guid.NewGuid();
        var card1 = new Card { Id = Guid.NewGuid(), Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card1 });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        var card2 = new Card { Id = Guid.NewGuid(), Number = 2, Title = "Beta", LaneName = "To Do", Description = "" };
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card1, card2 });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards.Should().HaveCount(2);
        vm.Lanes[0].Cards[1].Title.Should().Be("Beta");
    }

    [Fact]
    public async Task RefreshCommand_WhenLanesChanged_RebuildsAllLanes()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1), new("Doing", 2) });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes.Should().HaveCount(2);
        vm.Lanes[1].Name.Should().Be("Doing");
    }

    [Fact]
    public async Task LoadAsync_WithTagNameSetButTagMissingFromDictionary_SetsTagColourNull()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        var card = new Card { Id = Guid.NewGuid(), Number = 1, Title = "T", LaneName = "To Do", Description = "", TagName = "feature" };
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        var cardVm = vm.Lanes[0].Cards[0];
        cardVm.TagName.Should().Be("feature");
        cardVm.TagColour.Should().BeNull();
    }

    [Fact]
    public async Task RefreshCommand_CaseInsensitiveLaneNameMatch_TakesIncrementalPath()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var card = new Card { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        // Refresh with same lane but different casing — incremental path taken, card reference preserved
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("TO DO", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "TO DO", Description = "" } });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().BeSameAs(originalCardVm);
    }

    [Fact]
    public async Task RefreshCommand_WhenLanesUnchanged_ReorderedCards_UpdatesPositions()
    {
        var workspaceId = Guid.NewGuid();
        var cardAlphaId = Guid.NewGuid();
        var cardBetaId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>
            {
                new() { Id = cardAlphaId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" },
                new() { Id = cardBetaId, Number = 2, Title = "Beta", LaneName = "To Do", Description = "" },
            });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>
            {
                new() { Id = cardBetaId, Number = 2, Title = "Beta", LaneName = "To Do", Description = "" },
                new() { Id = cardAlphaId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" },
            });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards.Should().HaveCount(2);
        vm.Lanes[0].Cards[0].Title.Should().Be("Beta");
        vm.Lanes[0].Cards[1].Title.Should().Be("Alpha");
    }

    [Fact]
    public async Task RefreshCommand_WhenLanesUnchanged_AllCardsRemoved_EmptiesLane()
    {
        var workspaceId = Guid.NewGuid();
        var card = new Card { Id = Guid.NewGuid(), Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenListCardsByWorkspaceQueryThrows_PropagatesException()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<Card>>(new InvalidOperationException("query failed")));
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);

        await vm.Invoking(v => v.LoadAsync(workspaceId))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("query failed");
    }

    [Fact]
    public async Task LoadAsync_WhenLaneHasNoCards_LaneAppearsWithEmptyCardCollection()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        vm.Lanes.Should().HaveCount(1);
        vm.Lanes[0].Name.Should().Be("To Do");
        vm.Lanes[0].Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Matches_DescriptionChanged_CardIsReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "Old description" } });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "New description" } });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().NotBeSameAs(originalCardVm);
        vm.Lanes[0].Cards[0].Description.Should().Be("New description");
    }

    [Fact]
    public async Task Matches_TagNameChanged_CardIsReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", TagName = "feature" } });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("feature", "#7fa87a") });

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", TagName = "bug" } });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("feature", "#7fa87a"), new("bug", "#ff0000") });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().NotBeSameAs(originalCardVm);
        vm.Lanes[0].Cards[0].TagName.Should().Be("bug");
    }

    [Fact]
    public async Task Matches_TagColourChangedForSameTagName_CardIsReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", TagName = "feature" } });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("feature", "#7fa87a") });

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("feature", "#ff0000") });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().NotBeSameAs(originalCardVm);
        vm.Lanes[0].Cards[0].TagColour.Should().Be("#ff0000");
    }

    [Fact]
    public async Task Matches_TagColourLookupCaseInsensitive_CardNotReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", TagName = "feature" } });
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("feature", "#7fa87a") });

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        // Tag dictionary key in different casing but same colour — OrdinalIgnoreCase lookup resolves the same colour
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("FEATURE", "#7fa87a") });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().BeSameAs(originalCardVm);
    }

    [Fact]
    public async Task RefreshCommand_CanExecute_IsTrueAfterLoad()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());
        mediator.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator);
        await vm.LoadAsync(workspaceId);

        vm.RefreshCommand.CanExecute(null).Should().BeTrue();

        IAsyncRelayCommand command = vm.RefreshCommand;
        await command.ExecuteAsync(null);

        vm.Lanes.Should().HaveCount(1);
    }
}
