using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Git.Push;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Tags.ListTags;
using Bishop.App.Workspaces.LaunchPlainTerminal;
using Bishop.App.Workspaces.LaunchWorkspace;
using Bishop.App.Workspaces.SetWorkspaceGitHubRepo;
using Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;
using Bishop.Core;
using Bishop.Core.Skills;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Workspaces;

public class WorkspaceBoardViewModelTests
{
    [Fact]
    public void IsSearchEmpty_TrueWhenSearchTextIsEmpty()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());

        vm.IsSearchEmpty.Should().BeTrue();

        vm.SearchText = "lane";
        vm.IsSearchEmpty.Should().BeFalse();

        vm.SearchText = string.Empty;
        vm.IsSearchEmpty.Should().BeTrue();
    }

    [Fact]
    public void SearchText_RaisesIsSearchEmptyChanged()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsSearchEmpty.Should().BeTrue();
        vm.SearchText = "x";

        changed.Should().Contain(nameof(WorkspaceBoardViewModel.IsSearchEmpty));
        vm.IsSearchEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task SearchText_PropagatesFilterToLanes()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>
            {
                new() { Id = Guid.NewGuid(), Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" },
                new() { Id = Guid.NewGuid(), Number = 2, Title = "Beta", LaneName = "To Do", Description = "" },
            });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);

        vm.SearchText = "alpha";

        vm.Lanes[0].FilteredCards.Should().HaveCount(1);
        vm.Lanes[0].FilteredCards[0].Title.Should().Be("Alpha");
    }

    [Fact]
    public async Task LoadAsync_BuildsLanesAndCardsFromMediator()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1), new("Doing", 2) });
        var card = new Card { Id = Guid.NewGuid(), Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" };
        mediator.Send(Arg.Is<ListCardsByWorkspaceQuery>(q => q.LaneName == "To Do"), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });
        mediator.Send(Arg.Is<ListCardsByWorkspaceQuery>(q => q.LaneName == "Doing"), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("feature", "#7fa87a") });

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());

        await vm.Invoking(v => v.LoadAsync(workspaceId))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("query failed");
        vm.Lanes.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenListLanesByWorkspaceQueryThrows_PropagatesException()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<LaneInfo>>(new InvalidOperationException("lanes failed")));

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());

        await vm.Invoking(v => v.LoadAsync(workspaceId))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("lanes failed");
    }

    [Fact]
    public async Task LoadAsync_WhenListTagsQueryThrows_PropagatesException()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<TagInfo>>(new InvalidOperationException("tags failed")));

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());

        await vm.Invoking(v => v.LoadAsync(workspaceId))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("tags failed");
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("feature", "#7fa87a") });

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", TagName = "bug" } });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("feature", "#7fa87a") });

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo> { new("feature", "#7fa87a") });

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        // Tag dictionary key in different casing but same colour — OrdinalIgnoreCase lookup resolves the same colour
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
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
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);

        vm.RefreshCommand.CanExecute(null).Should().BeTrue();

        IAsyncRelayCommand command = vm.RefreshCommand;
        await command.ExecuteAsync(null);

        vm.Lanes.Should().HaveCount(1);
    }

    [Fact]
    public void HasSelection_FalseWhenNoCardsSelected()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        lane.Cards.Add(new CardViewModel { Title = "Alpha" });
        vm.Lanes.Add(lane);

        vm.HasSelection.Should().BeFalse();
        vm.SelectionCount.Should().Be(0);
    }

    [Fact]
    public void ToggleCardSelection_SelectsUnselectedCard()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card = new CardViewModel { Title = "Alpha" };
        lane.Cards.Add(card);
        vm.Lanes.Add(lane);

        vm.ToggleCardSelection(card);

        card.IsSelected.Should().BeTrue();
        vm.HasSelection.Should().BeTrue();
        vm.SelectionCount.Should().Be(1);
    }

    [Fact]
    public void ToggleCardSelection_DeselectsSelectedCard()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card = new CardViewModel { Title = "Alpha" };
        lane.Cards.Add(card);
        vm.Lanes.Add(lane);
        vm.ToggleCardSelection(card);

        vm.ToggleCardSelection(card);

        card.IsSelected.Should().BeFalse();
        vm.HasSelection.Should().BeFalse();
    }

    [Fact]
    public void ClearSelection_DeselectsAllCards()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card1 = new CardViewModel { Title = "Alpha" };
        var card2 = new CardViewModel { Title = "Beta" };
        lane.Cards.Add(card1);
        lane.Cards.Add(card2);
        vm.Lanes.Add(lane);
        vm.ToggleCardSelection(card1);
        vm.ToggleCardSelection(card2);

        vm.ClearSelection();

        card1.IsSelected.Should().BeFalse();
        card2.IsSelected.Should().BeFalse();
        vm.HasSelection.Should().BeFalse();
        vm.SelectionCount.Should().Be(0);
    }

    [Fact]
    public void SelectedCards_ReturnsOnlySelectedCards()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card1 = new CardViewModel { Title = "Alpha" };
        var card2 = new CardViewModel { Title = "Beta" };
        lane.Cards.Add(card1);
        lane.Cards.Add(card2);
        vm.Lanes.Add(lane);

        vm.ToggleCardSelection(card1);

        vm.SelectedCards.Should().ContainSingle().Which.Title.Should().Be("Alpha");
    }

    [Fact]
    public void ToggleCardSelection_AddsCardToStagingTray()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card = new CardViewModel { Title = "Alpha" };
        lane.Cards.Add(card);
        vm.Lanes.Add(lane);

        vm.ToggleCardSelection(card);

        vm.StagingTray.Cards.Should().ContainSingle().Which.Should().BeSameAs(card);
        vm.StagingTray.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void ToggleCardSelection_RemovesCardFromStagingTrayOnDeselect()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card = new CardViewModel { Title = "Alpha" };
        lane.Cards.Add(card);
        vm.Lanes.Add(lane);
        vm.ToggleCardSelection(card);

        vm.ToggleCardSelection(card);

        vm.StagingTray.Cards.Should().BeEmpty();
        vm.StagingTray.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void ClearSelection_ResetsStagingTray()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card = new CardViewModel { Title = "Alpha" };
        lane.Cards.Add(card);
        vm.Lanes.Add(lane);
        vm.ToggleCardSelection(card);
        vm.StagingTray.Name = "draft";

        vm.ClearSelection();

        vm.StagingTray.Cards.Should().BeEmpty();
        vm.StagingTray.Name.Should().BeEmpty();
    }

    [Fact]
    public void SelectionLabel_ShowsCountWithSingularForOneCard()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card = new CardViewModel { Title = "Alpha" };
        lane.Cards.Add(card);
        vm.Lanes.Add(lane);

        vm.ToggleCardSelection(card);

        vm.SelectionLabel.Should().Be("1 selected");
    }

    [Fact]
    public void SelectionLabel_ShowsPluralForMultipleCards()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card1 = new CardViewModel { Title = "Alpha" };
        var card2 = new CardViewModel { Title = "Beta" };
        lane.Cards.Add(card1);
        lane.Cards.Add(card2);
        vm.Lanes.Add(lane);

        vm.ToggleCardSelection(card1);
        vm.ToggleCardSelection(card2);

        vm.SelectionLabel.Should().Be("2 selected");
    }

    [Fact]
    public void ToggleCardSelection_RaisesPropertyChangedForHasSelection()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card = new CardViewModel { Title = "Alpha" };
        lane.Cards.Add(card);
        vm.Lanes.Add(lane);
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ToggleCardSelection(card);

        changed.Should().Contain(nameof(WorkspaceBoardViewModel.HasSelection));
        changed.Should().Contain(nameof(WorkspaceBoardViewModel.SelectionCount));
    }

    [Fact]
    public async Task LoadAsync_WithBatchedCards_GroupsThemInLaneItems()
    {
        var workspaceId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var batch = new Bishop.Core.Batch { Id = batchId, Name = "Sprint 1" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>
            {
                new() { Id = Guid.NewGuid(), Number = 1, Title = "Standalone", LaneName = "To Do", Description = "" },
                new() { Id = Guid.NewGuid(), Number = 2, Title = "Batch-1", LaneName = "To Do", Description = "", BatchId = batchId, Batch = batch },
                new() { Id = Guid.NewGuid(), Number = 3, Title = "Batch-2", LaneName = "To Do", Description = "", BatchId = batchId, Batch = batch },
            });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);

        var lane = vm.Lanes[0];
        lane.LaneItems.Should().HaveCount(2);
        lane.LaneItems[0].Should().BeOfType<CardViewModel>().Which.Title.Should().Be("Standalone");
        var group = lane.LaneItems[1].Should().BeOfType<BatchGroupViewModel>().Subject;
        group.BatchName.Should().Be("Sprint 1");
        group.Cards.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_BatchStats_TotalCountSpansAllLanes()
    {
        var workspaceId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var batch = new Bishop.Core.Batch { Id = batchId, Name = "B" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1), new("Done", 2) });
        mediator.Send(Arg.Is<ListCardsByWorkspaceQuery>(q => q.LaneName == "To Do"), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = Guid.NewGuid(), Number = 1, Title = "C1", LaneName = "To Do", Description = "", BatchId = batchId, Batch = batch } });
        mediator.Send(Arg.Is<ListCardsByWorkspaceQuery>(q => q.LaneName == "Done"), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = Guid.NewGuid(), Number = 2, Title = "C2", LaneName = "Done", Description = "", BatchId = batchId, Batch = batch } });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);

        var group = vm.Lanes[0].LaneItems[0].Should().BeOfType<BatchGroupViewModel>().Subject;
        group.TotalCount.Should().Be(2);
        group.DoneCount.Should().Be(1);
        group.ProgressDisplay.Should().Be("(1/2)");
    }

    [Fact]
    public void Lanes_IsEmptyBeforeLoadAsync()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());

        vm.Lanes.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WithIsCardSkillsButtonVisible_SetsSkillsButtonVisibleOnCards()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        var card = new Card { Id = Guid.NewGuid(), Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" };
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>()) { IsCardSkillsButtonVisible = true };
        await vm.LoadAsync(workspaceId);

        vm.Lanes[0].Cards[0].IsSkillsButtonVisible.Should().BeTrue();
    }

    [Fact]
    public async Task SearchText_UpdatesBatchStatsInLaneItems()
    {
        var workspaceId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var batch = new Bishop.Core.Batch { Id = batchId, Name = "Sprint 1" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1), new("Done", 2) });
        mediator.Send(Arg.Is<ListCardsByWorkspaceQuery>(q => q.LaneName == "To Do"), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = Guid.NewGuid(), Number = 1, Title = "Batch Alpha", LaneName = "To Do", Description = "", BatchId = batchId, Batch = batch } });
        mediator.Send(Arg.Is<ListCardsByWorkspaceQuery>(q => q.LaneName == "Done"), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = Guid.NewGuid(), Number = 2, Title = "Batch Beta", LaneName = "Done", Description = "", BatchId = batchId, Batch = batch } });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);

        vm.SearchText = "Batch";

        var group = vm.Lanes[0].LaneItems[0].Should().BeOfType<BatchGroupViewModel>().Subject;
        group.TotalCount.Should().Be(2);
        group.DoneCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_MultipleBatches_AssignSequentialAccentIndicesByCreatedAt()
    {
        var workspaceId = Guid.NewGuid();
        var batchOldId = Guid.NewGuid();
        var batchNewId = Guid.NewGuid();
        var batchOld = new Bishop.Core.Batch { Id = batchOldId, Name = "Old", CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        var batchNew = new Bishop.Core.Batch { Id = batchNewId, Name = "New", CreatedAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero) };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>
            {
                new() { Id = Guid.NewGuid(), Number = 2, Title = "New-1", LaneName = "To Do", Description = "", BatchId = batchNewId, Batch = batchNew },
                new() { Id = Guid.NewGuid(), Number = 1, Title = "Old-1", LaneName = "To Do", Description = "", BatchId = batchOldId, Batch = batchOld },
            });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);

        var items = vm.Lanes[0].LaneItems;
        var groupNew = items.OfType<BatchGroupViewModel>().First(g => g.BatchName == "New");
        var groupOld = items.OfType<BatchGroupViewModel>().First(g => g.BatchName == "Old");
        groupOld.AccentIndex.Should().Be(0);
        groupNew.AccentIndex.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_SameBatchInMultipleLanes_HasSameAccentIndex()
    {
        var workspaceId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var batch = new Bishop.Core.Batch { Id = batchId, Name = "Sprint", CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1), new("Doing", 2) });
        mediator.Send(Arg.Is<ListCardsByWorkspaceQuery>(q => q.LaneName == "To Do"), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = Guid.NewGuid(), Number = 1, Title = "C1", LaneName = "To Do", Description = "", BatchId = batchId, Batch = batch } });
        mediator.Send(Arg.Is<ListCardsByWorkspaceQuery>(q => q.LaneName == "Doing"), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = Guid.NewGuid(), Number = 2, Title = "C2", LaneName = "Doing", Description = "", BatchId = batchId, Batch = batch } });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);

        var groupToDo = vm.Lanes[0].LaneItems[0].Should().BeOfType<BatchGroupViewModel>().Subject;
        var groupDoing = vm.Lanes[1].LaneItems[0].Should().BeOfType<BatchGroupViewModel>().Subject;
        groupToDo.AccentIndex.Should().Be(groupDoing.AccentIndex);
    }

    [Fact]
    public async Task Matches_BatchIdChanged_CardIsReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" } });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        var batchId = Guid.NewGuid();
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", BatchId = batchId } });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().NotBeSameAs(originalCardVm);
        vm.Lanes[0].Cards[0].BatchId.Should().Be(batchId);
    }

    [Fact]
    public async Task LoadAsync_WhenCalledWithDifferentWorkspaceId_ClearsOldLanes()
    {
        var workspaceId1 = Guid.NewGuid();
        var workspaceId2 = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1), new("Doing", 2) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId1);

        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("Backlog", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = Guid.NewGuid(), Number = 1, Title = "New Card", LaneName = "Backlog", Description = "" } });
        await vm.LoadAsync(workspaceId2);

        vm.Lanes.Should().HaveCount(1);
        vm.Lanes[0].Name.Should().Be("Backlog");
        vm.Lanes[0].Cards.Should().HaveCount(1);
        vm.Lanes[0].Cards[0].Title.Should().Be("New Card");
    }

    [Fact]
    public async Task SearchText_ClearingAfterFilter_ShowsAllCards()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>
            {
                new() { Id = Guid.NewGuid(), Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" },
                new() { Id = Guid.NewGuid(), Number = 2, Title = "Beta", LaneName = "To Do", Description = "" },
            });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        vm.SearchText = "alpha";

        vm.SearchText = string.Empty;

        vm.Lanes[0].FilteredCards.Should().HaveCount(2);
    }

    [Fact]
    public async Task ComputeBatchStats_WhenBatchHasNullCreatedAt_UsesMaxValueAsDefault()
    {
        var workspaceId = Guid.NewGuid();
        var batchWithDateId = Guid.NewGuid();
        var batchNullDateId = Guid.NewGuid();
        var batchWithDate = new Bishop.Core.Batch { Id = batchWithDateId, Name = "Dated", CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>
            {
                new() { Id = Guid.NewGuid(), Number = 1, Title = "Dated-1", LaneName = "To Do", Description = "", BatchId = batchWithDateId, Batch = batchWithDate },
                new() { Id = Guid.NewGuid(), Number = 2, Title = "Undated-1", LaneName = "To Do", Description = "", BatchId = batchNullDateId },
            });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);

        var groupDated = vm.Lanes[0].LaneItems.OfType<BatchGroupViewModel>().First(g => g.BatchId == batchWithDateId);
        var groupUndated = vm.Lanes[0].LaneItems.OfType<BatchGroupViewModel>().First(g => g.BatchId == batchNullDateId);
        groupDated.AccentIndex.Should().Be(0);
        groupUndated.AccentIndex.Should().Be(1);
    }

    [Fact]
    public async Task Matches_IdChanged_CardIsReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = originalId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" } });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        var newId = Guid.NewGuid();
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = newId, Number = 2, Title = "Alpha", LaneName = "To Do", Description = "" } });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().NotBeSameAs(originalCardVm);
        vm.Lanes[0].Cards[0].Id.Should().Be(newId);
    }

    [Fact]
    public async Task Matches_IsClosedChanged_CardIsReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", IsClosed = false } });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", IsClosed = true } });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().NotBeSameAs(originalCardVm);
        vm.Lanes[0].Cards[0].IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task Matches_GitHubIssueNumberChanged_CardIsReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" } });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", GitHubIssueNumber = 42 } });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().NotBeSameAs(originalCardVm);
        vm.Lanes[0].Cards[0].GitHubIssueNumber.Should().Be(42);
    }

    [Fact]
    public async Task Matches_GitHubPushedAtChanged_CardIsReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var pushedAt = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" } });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", GitHubPushedAt = pushedAt } });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().NotBeSameAs(originalCardVm);
        vm.Lanes[0].Cards[0].GitHubPushedAt.Should().Be(pushedAt);
    }

    [Fact]
    public async Task Matches_LastAutoRunFailedAtChanged_CardIsReplaced()
    {
        var workspaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var failedAt = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "" } });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        var originalCardVm = vm.Lanes[0].Cards[0];

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { new() { Id = cardId, Number = 1, Title = "Alpha", LaneName = "To Do", Description = "", LastAutoRunFailedAt = failedAt } });
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Lanes[0].Cards[0].Should().NotBeSameAs(originalCardVm);
        vm.Lanes[0].Cards[0].LastAutoRunFailedAt.Should().Be(failedAt);
    }

    [Fact]
    public async Task RefreshCommand_WhenListCardsByWorkspaceQueryThrows_PropagatesException()
    {
        var workspaceId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);

        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<Card>>(new InvalidOperationException("refresh cards failed")));

        await vm.Invoking(v => v.RefreshCommand.ExecuteAsync(null))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("refresh cards failed");
    }

    [Fact]
    public async Task RefreshLaneItems_WithActiveFilter_LaneItemsShowOnlyFilteredCards()
    {
        var workspaceId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var batch = new Bishop.Core.Batch { Id = batchId, Name = "Sprint 1" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>
            {
                new() { Id = Guid.NewGuid(), Number = 1, Title = "Standalone Alpha", LaneName = "To Do", Description = "" },
                new() { Id = Guid.NewGuid(), Number = 2, Title = "Batch Beta", LaneName = "To Do", Description = "", BatchId = batchId, Batch = batch },
            });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);
        vm.SearchText = "Batch";

        vm.RefreshLaneItems();

        var lane = vm.Lanes[0];
        lane.FilteredCards.Should().HaveCount(1);
        lane.FilteredCards[0].Title.Should().Be("Batch Beta");
        lane.LaneItems.Should().HaveCount(1);
        lane.LaneItems[0].Should().BeOfType<BatchGroupViewModel>();
    }

    [Fact]
    public void ClearSelection_DeselectsCardsAcrossMultipleLanes()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var lane1 = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var lane2 = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "Doing" };
        var card1 = new CardViewModel { Title = "Alpha" };
        var card2 = new CardViewModel { Title = "Beta" };
        var card3 = new CardViewModel { Title = "Gamma" };
        lane1.Cards.Add(card1);
        lane1.Cards.Add(card2);
        lane2.Cards.Add(card3);
        vm.Lanes.Add(lane1);
        vm.Lanes.Add(lane2);
        vm.ToggleCardSelection(card1);
        vm.ToggleCardSelection(card2);
        vm.ToggleCardSelection(card3);

        vm.ClearSelection();

        card1.IsSelected.Should().BeFalse();
        card2.IsSelected.Should().BeFalse();
        card3.IsSelected.Should().BeFalse();
        vm.HasSelection.Should().BeFalse();
        vm.SelectionCount.Should().Be(0);
    }

    [Fact]
    public void ClearSelection_RaisesPropertyChangedForAllSelectionProperties()
    {
        var vm = new WorkspaceBoardViewModel(Substitute.For<IMediator>(), Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        var lane = new LaneViewModel(Substitute.For<IMediator>(), () => Task.CompletedTask) { Name = "To Do" };
        var card1 = new CardViewModel { Title = "Alpha" };
        var card2 = new CardViewModel { Title = "Beta" };
        var card3 = new CardViewModel { Title = "Gamma" };
        lane.Cards.Add(card1);
        lane.Cards.Add(card2);
        lane.Cards.Add(card3);
        vm.Lanes.Add(lane);
        vm.ToggleCardSelection(card1);
        vm.ToggleCardSelection(card2);
        vm.ToggleCardSelection(card3);
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ClearSelection();

        changed.Should().Contain(nameof(WorkspaceBoardViewModel.SelectedCards));
        changed.Should().Contain(nameof(WorkspaceBoardViewModel.SelectionCount));
        changed.Should().Contain(nameof(WorkspaceBoardViewModel.HasSelection));
        changed.Should().Contain(nameof(WorkspaceBoardViewModel.SelectionLabel));
    }

    [Fact]
    public async Task RefreshLaneItems_AfterCardSwap_RebuildsLaneItemsWithBatchGroup()
    {
        var workspaceId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var batch = new Bishop.Core.Batch { Id = batchId, Name = "Sprint 1" };
        var cardId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<LaneInfo> { new("To Do", 1) });
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>
            {
                new() { Id = cardId, Number = 1, Title = "Batch Card", LaneName = "To Do", Description = "", BatchId = batchId, Batch = batch },
            });
        mediator.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TagInfo>());

        var vm = new WorkspaceBoardViewModel(mediator, Substitute.For<Bishop.App.Services.Settings.IAppSettings>());
        await vm.LoadAsync(workspaceId);

        var lane = vm.Lanes[0];
        var original = lane.Cards[0];
        lane.Cards[0] = new CardViewModel
        {
            Id = original.Id,
            Number = original.Number,
            Title = original.Title,
            LaneName = original.LaneName,
            IsClosed = !original.IsClosed,
            BatchId = original.BatchId,
            BatchName = original.BatchName,
            BatchCreatedAt = original.BatchCreatedAt,
        };

        vm.RefreshLaneItems();

        var group = lane.LaneItems[0].Should().BeOfType<BatchGroupViewModel>().Subject;
        group.BatchName.Should().Be("Sprint 1");
        group.Cards.Should().HaveCount(1);
        group.Cards[0].IsClosed.Should().BeTrue();
    }

    // ── New delegating methods ────────────────────────────────────────────────

    private static (WorkspaceBoardViewModel vm, IMediator mediator, IAppSettings appSettings) MakeVm()
    {
        var mediator = Substitute.For<IMediator>();
        var appSettings = Substitute.For<IAppSettings>();
        return (new WorkspaceBoardViewModel(mediator, appSettings), mediator, appSettings);
    }

    [Fact]
    public async Task LoadSkillsAsync_SendsDiscoverSkillsQuery_AndPopulatesSkillArrays()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-work-on-card", "Work on card", ["card"], "/bish-work-on-card {{card_number}}"),
            });

        await vm.LoadSkillsAsync();

        await mediator.Received(1).Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>());
        vm.CardSkills.Should().HaveCount(1);
        vm.IsCardSkillsButtonVisible.Should().BeTrue();
    }

    [Fact]
    public async Task LaunchClaudeAsync_SendsLaunchWorkspaceCommand()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<LaunchWorkspaceCommand>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await vm.LaunchClaudeAsync(@"C:\repo", new TerminalSnap());

        result.Should().BeTrue();
        await mediator.Received(1).Send(
            Arg.Is<LaunchWorkspaceCommand>(c => c.Path == @"C:\repo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchTerminalAsync_SendsLaunchPlainTerminalCommand()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<LaunchPlainTerminalCommand>(), Arg.Any<CancellationToken>()).Returns(true);

        await vm.LaunchTerminalAsync(@"C:\repo", new TerminalSnap());

        await mediator.Received(1).Send(
            Arg.Is<LaunchPlainTerminalCommand>(c => c.Path == @"C:\repo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRecentCommitsAsync_NoCommits_MapsToVmType()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<GetRecentCommitsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetRecentCommitsResult.NoCommits());

        var result = await vm.GetRecentCommitsAsync(@"C:\repo");

        result.Should().BeOfType<RecentCommitsResult.NoCommits>();
        await mediator.Received(1).Send(
            Arg.Is<GetRecentCommitsQuery>(q => q.WorkspacePath == @"C:\repo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRecentCommitsAsync_NotAGitRepo_MapsToVmType()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<GetRecentCommitsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetRecentCommitsResult.NotAGitRepo());

        var result = await vm.GetRecentCommitsAsync(@"C:\repo");

        result.Should().BeOfType<RecentCommitsResult.NotAGitRepo>();
    }

    [Fact]
    public async Task GetRecentCommitsAsync_GitNotFound_MapsToVmType()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<GetRecentCommitsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetRecentCommitsResult.GitNotFound());

        var result = await vm.GetRecentCommitsAsync(@"C:\repo");

        result.Should().BeOfType<RecentCommitsResult.GitNotFound>();
    }

    [Fact]
    public async Task GetRecentCommitsAsync_Success_MapsCommitsAndMetadata()
    {
        var (vm, mediator, _) = MakeVm();
        var timestamp = new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);
        var appCommit = new Bishop.App.Git.CommitInfo(
            "abc1234", "abc1234deadbeef", "Subject line", "Body text", timestamp, IsPushed: true);
        mediator.Send(Arg.Any<GetRecentCommitsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetRecentCommitsResult.Success(
                [appCommit],
                UpstreamRef: "origin/main",
                UpstreamIsTracked: false,
                UnpushedCount: 3));

        var result = await vm.GetRecentCommitsAsync(@"C:\repo");

        var success = result.Should().BeOfType<RecentCommitsResult.Success>().Subject;
        success.UpstreamRef.Should().Be("origin/main");
        success.UpstreamIsTracked.Should().BeFalse();
        success.UnpushedCount.Should().Be(3);
        success.Commits.Should().HaveCount(1);
        var mapped = success.Commits[0];
        mapped.ShortHash.Should().Be("abc1234");
        mapped.FullHash.Should().Be("abc1234deadbeef");
        mapped.Subject.Should().Be("Subject line");
        mapped.Body.Should().Be("Body text");
        mapped.Timestamp.Should().Be(timestamp);
        mapped.IsPushed.Should().BeTrue();
    }

    [Fact]
    public async Task PushAsync_SendsPushCommand_MapsResult()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<PushCommand>(), Arg.Any<CancellationToken>())
            .Returns(new PushResult(true, "ok"));

        var result = await vm.PushAsync(@"C:\repo");

        result.Should().BeOfType<PushOutcome>();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("ok");
        await mediator.Received(1).Send(
            Arg.Is<PushCommand>(c => c.WorkspacePath == @"C:\repo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushAsync_Failure_PropagatesMessage()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<PushCommand>(), Arg.Any<CancellationToken>())
            .Returns(new PushResult(false, "rejected"));

        var result = await vm.PushAsync(@"C:\repo", setUpstream: true);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("rejected");
        await mediator.Received(1).Send(
            Arg.Is<PushCommand>(c => c.WorkspacePath == @"C:\repo" && c.SetUpstream),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleCardClosedAsync_WhenOpen_SendsCloseCardCommand()
    {
        var (vm, mediator, _) = MakeVm();
        var cardId = Guid.NewGuid();
        mediator.Send(Arg.Any<CloseCardCommand>(), Arg.Any<CancellationToken>()).Returns(new Bishop.Core.Card());

        await vm.ToggleCardClosedAsync(cardId, isClosed: false);

        await mediator.Received(1).Send(
            Arg.Is<CloseCardCommand>(c => c.CardId == cardId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleCardClosedAsync_WhenClosed_SendsReopenCardCommand()
    {
        var (vm, mediator, _) = MakeVm();
        var cardId = Guid.NewGuid();
        mediator.Send(Arg.Any<ReopenCardCommand>(), Arg.Any<CancellationToken>()).Returns(new Bishop.Core.Card());

        await vm.ToggleCardClosedAsync(cardId, isClosed: true);

        await mediator.Received(1).Send(
            Arg.Is<ReopenCardCommand>(c => c.CardId == cardId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveCardAsync_SendsMoveCardCommand()
    {
        var (vm, mediator, _) = MakeVm();
        var cardId = Guid.NewGuid();
        mediator.Send(Arg.Any<MoveCardCommand>(), Arg.Any<CancellationToken>()).Returns(new Bishop.Core.Card());

        await vm.MoveCardAsync(cardId, "Doing", 1);

        await mediator.Received(1).Send(
            Arg.Is<MoveCardCommand>(c => c.CardId == cardId && c.ToLaneName == "Doing" && c.ToPosition == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCardTagAsync_SendsUpdateCardCommand()
    {
        var (vm, mediator, _) = MakeVm();
        var cardId = Guid.NewGuid();
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>()).Returns(new Bishop.Core.Card());

        await vm.UpdateCardTagAsync(cardId, "feature");

        await mediator.Received(1).Send(
            Arg.Is<UpdateCardCommand>(c => c.CardId == cardId && c.TagName == "feature"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetGitHubRepoAsync_SendsSetWorkspaceGitHubRepoCommand()
    {
        var (vm, mediator, _) = MakeVm();
        var workspaceId = Guid.NewGuid();
        mediator.Send(Arg.Any<SetWorkspaceGitHubRepoCommand>(), Arg.Any<CancellationToken>()).Returns(new Bishop.Core.Workspace());

        await vm.SetGitHubRepoAsync(workspaceId, "owner/repo");

        await mediator.Received(1).Send(
            Arg.Is<SetWorkspaceGitHubRepoCommand>(c => c.WorkspaceId == workspaceId && c.Repo == "owner/repo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsetGitHubRepoAsync_SendsUnsetWorkspaceGitHubRepoCommand()
    {
        var (vm, mediator, _) = MakeVm();
        var workspaceId = Guid.NewGuid();
        mediator.Send(Arg.Any<UnsetWorkspaceGitHubRepoCommand>(), Arg.Any<CancellationToken>()).Returns(new Bishop.Core.Workspace());

        await vm.UnsetGitHubRepoAsync(workspaceId);

        await mediator.Received(1).Send(
            Arg.Is<UnsetWorkspaceGitHubRepoCommand>(c => c.WorkspaceId == workspaceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchAsync_UsesWorkspacePathAndRenderedCommand()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<LaunchSkillCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        vm.WorkspacePath = @"C:\repo";

        var item = new SkillLaunchItem("bish-arch", null, "claude-sonnet-4-6",
            RenderedCommand: "/bish-arch", RequiresStage: false, StagePrompt: null, StagePrefill: null, MarkdownBody: "");

        await vm.LaunchAsync(item, stagedText: null, new TerminalSnap(), "claude-opus-4-7");

        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c =>
                c.WorkspacePath == @"C:\repo" &&
                c.RenderedCommand == "/bish-arch" &&
                c.ModelId == "claude-opus-4-7"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchAsync_AppendsStagedTextToRenderedCommand()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<LaunchSkillCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        vm.WorkspacePath = @"C:\repo";

        var item = new SkillLaunchItem("bish-write-skill", null, "claude-sonnet-4-6",
            RenderedCommand: "/bish-write-skill", RequiresStage: true, StagePrompt: null, StagePrefill: null, MarkdownBody: "");

        await vm.LaunchAsync(item, stagedText: "new-name", new TerminalSnap(), "claude-sonnet-4-6");

        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c => c.RenderedCommand == "/bish-write-skill new-name"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildWorkspaceSkillLaunchItemsAsync_RendersWithoutCardContextAndPropagatesSavedModel()
    {
        var (vm, mediator, appSettings) = MakeVm();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-arch", "", ["workspace"], "/bish-arch --cd {{workspace_path}}"),
            });
        appSettings.GetAsync("skill.bish-arch.last_model", Arg.Any<CancellationToken>())
            .Returns("claude-opus-4-7");
        await vm.LoadSkillsAsync();
        vm.WorkspacePath = @"C:\repo";

        var items = await vm.BuildWorkspaceSkillLaunchItemsAsync();

        items.Should().ContainSingle();
        items[0].RenderedCommand.Should().Be(@"/bish-arch --cd C:\repo");
        items[0].SavedModelId.Should().Be("claude-opus-4-7");
    }

    [Fact]
    public async Task BuildCardSkillLaunchItemsAsync_RendersCardPlaceholders()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-work-on-card", "", ["card"], "/bish-work-on-card {{card_number}}"),
            });
        await vm.LoadSkillsAsync();
        vm.WorkspacePath = @"C:\repo";

        var card = new CardViewModel { Id = Guid.NewGuid(), Number = 42, Title = "T", Description = "D", LaneName = "Doing" };
        var items = await vm.BuildCardSkillLaunchItemsAsync(card);

        items.Should().ContainSingle()
            .Which.RenderedCommand.Should().Be("/bish-work-on-card 42");
    }

    [Fact]
    public async Task LaunchWorkspaceSkillByNameAsync_LooksUpAndLaunchesByName()
    {
        var (vm, mediator, _) = MakeVm();
        mediator.Send(Arg.Any<DiscoverSkillsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InstalledSkill>
            {
                new("bish-arch", "", ["workspace"], "/bish-arch"),
            });
        mediator.Send(Arg.Any<LaunchSkillCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        await vm.LoadSkillsAsync();
        vm.WorkspacePath = @"C:\repo";

        await vm.LaunchWorkspaceSkillByNameAsync("bish-arch", "claude-opus-4-7", new TerminalSnap());

        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c =>
                c.WorkspacePath == @"C:\repo" &&
                c.RenderedCommand == "/bish-arch" &&
                c.ModelId == "claude-opus-4-7"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchWorkspaceSkillByNameAsync_UnknownNameIsNoOp()
    {
        var (vm, mediator, _) = MakeVm();
        await vm.LaunchWorkspaceSkillByNameAsync("missing", "claude-sonnet-4-6", new TerminalSnap());

        await mediator.DidNotReceive().Send(Arg.Any<LaunchSkillCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetSkillModelAsync_WritesToAppSettings()
    {
        var (vm, _, appSettings) = MakeVm();

        await vm.SetSkillModelAsync("bish-arch", "claude-opus-4-7");

        await appSettings.Received(1).SetAsync("skill.bish-arch.last_model", "claude-opus-4-7", Arg.Any<CancellationToken>());
    }
}
