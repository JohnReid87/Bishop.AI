using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.Core;
using Bishop.ViewModels;
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
}
