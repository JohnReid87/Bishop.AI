using Bishop.App.Cards.AddCard;
using Bishop.Core;
using Bishop.ViewModels;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class LaneViewModelTests
{
    [Theory]
    [InlineData("To Do", true, false, false)]
    [InlineData("Backlog", false, true, false)]
    [InlineData("Done", false, false, true)]
    [InlineData("Doing", false, false, false)]
    public void LaneFlags_MatchName(string name, bool isToDo, bool isBacklog, bool isDone)
    {
        var vm = NewVm(name: name);

        vm.IsToDoLane.Should().Be(isToDo);
        vm.IsBacklogLane.Should().Be(isBacklog);
        vm.IsDoneLane.Should().Be(isDone);
    }

    [Fact]
    public void DisplayName_IncludesNameAndFilteredCount()
    {
        var vm = NewVm(name: "To Do");
        vm.DisplayName.Should().Be("To Do (0)");

        vm.Cards.Add(new CardViewModel { Title = "A", LaneName = "To Do" });

        vm.DisplayName.Should().Be("To Do (1)");
    }

    [Fact]
    public void CanWorkNext_TrueForToDoLaneWithCards()
    {
        var vm = NewVm(name: "To Do");
        vm.CanWorkNext.Should().BeFalse();

        vm.Cards.Add(new CardViewModel { Title = "A" });

        vm.CanWorkNext.Should().BeTrue();
    }

    [Fact]
    public void WorkNextTooltip_ChangesWithCanWorkNext()
    {
        var vm = NewVm(name: "To Do");
        vm.WorkNextTooltip.Should().Be("No cards in To Do");

        vm.Cards.Add(new CardViewModel { Title = "A" });

        vm.WorkNextTooltip.Should().Be("Ralph it");
    }

    [Fact]
    public void IsImportVisible_TrueForBacklogWithGitHubRepo()
    {
        var vm = NewVm(name: "Backlog");
        vm.IsImportVisible.Should().BeFalse();

        vm.HasGitHubRepo = true;

        vm.IsImportVisible.Should().BeTrue();
    }

    [Fact]
    public void IsPushToGitHubVisible_TrueForDoneLaneWithGitHubRepo()
    {
        var vm = NewVm(name: "Done");
        vm.IsPushToGitHubVisible.Should().BeFalse();

        vm.HasGitHubRepo = true;

        vm.IsPushToGitHubVisible.Should().BeTrue();
    }

    [Fact]
    public void IsPlayVisible_TrueForToDoWhenNotRunning()
    {
        var vm = NewVm(name: "To Do");
        vm.IsPlayVisible.Should().BeTrue();
        vm.IsStopVisible.Should().BeFalse();

        vm.IsWorkNextRunning = true;

        vm.IsPlayVisible.Should().BeFalse();
        vm.IsStopVisible.Should().BeTrue();
    }

    [Fact]
    public void CanPlayWorkNext_FalseWhileRunning()
    {
        var vm = NewVm(name: "To Do");
        vm.Cards.Add(new CardViewModel { Title = "A" });
        vm.CanPlayWorkNext.Should().BeTrue();

        vm.IsWorkNextRunning = true;

        vm.CanPlayWorkNext.Should().BeFalse();
    }

    [Fact]
    public void CanStopWorkNext_TrueOnlyWhileRunningAndNotStopping()
    {
        var vm = NewVm();
        vm.IsWorkNextRunning = true;
        vm.CanStopWorkNext.Should().BeTrue();

        vm.IsWorkNextStopping = true;

        vm.CanStopWorkNext.Should().BeFalse();
    }

    [Fact]
    public void StopWorkNextTooltip_ChangesWhileStopping()
    {
        var vm = NewVm();
        vm.StopWorkNextTooltip.Should().Be("Stop Ralphing");

        vm.IsWorkNextStopping = true;

        vm.StopWorkNextTooltip.Should().Be("Stopping Ralph…");
    }

    [Fact]
    public void HasAddCardError_TrueWhenMessageIsSet()
    {
        var vm = NewVm();
        vm.HasAddCardError.Should().BeFalse();

        vm.AddCardErrorMessage = "Something went wrong";
        vm.HasAddCardError.Should().BeTrue();

        vm.AddCardErrorMessage = null;
        vm.HasAddCardError.Should().BeFalse();
    }

    [Fact]
    public void BeginAddCardCommand_SetsIsAddingCardAndClearsError()
    {
        var vm = NewVm();
        vm.IsAddingCard.Should().BeFalse();

        vm.BeginAddCardCommand.Execute(null);

        vm.IsAddingCard.Should().BeTrue();
        vm.AddCardErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CancelAddCardCommand_ResetsAddingState()
    {
        var vm = NewVm();
        vm.IsAddingCard = true;
        vm.NewCardTitle = "Draft";
        vm.AddCardErrorMessage = "Error";

        vm.CancelAddCardCommand.Execute(null);

        vm.IsAddingCard.Should().BeFalse();
        vm.NewCardTitle.Should().BeEmpty();
        vm.AddCardErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ApplyFilter_FiltersCardsByTitle()
    {
        var vm = NewVm(name: "To Do");
        vm.Cards.Add(new CardViewModel { Title = "Lane management" });
        vm.Cards.Add(new CardViewModel { Title = "Card detail" });

        vm.ApplyFilter("lane");

        vm.FilteredCards.Should().HaveCount(1);
        vm.FilteredCards[0].Title.Should().Be("Lane management");
    }

    [Fact]
    public void ApplyFilter_EmptyString_ShowsAllCards()
    {
        var vm = NewVm(name: "To Do");
        vm.Cards.Add(new CardViewModel { Title = "A" });
        vm.Cards.Add(new CardViewModel { Title = "B" });
        vm.ApplyFilter("A");

        vm.ApplyFilter(string.Empty);

        vm.FilteredCards.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConfirmAddCardAsync_DoesNothingForBlankTitle()
    {
        var mediator = Substitute.For<IMediator>();
        var vm = NewVm(mediator: mediator);
        vm.NewCardTitle = "   ";

        await vm.ConfirmAddCardCommand.ExecuteAsync(null);

        await mediator.DidNotReceive().Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmAddCardAsync_SendsCommandAndRefreshesBoard()
    {
        var refreshed = false;
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Card { Id = Guid.NewGuid(), Title = "New card", LaneName = "To Do" });
        var vm = NewVm(mediator: mediator, refreshBoard: () => { refreshed = true; return Task.CompletedTask; });
        vm.NewCardTitle = "New card";

        await vm.ConfirmAddCardCommand.ExecuteAsync(null);

        vm.IsAddingCard.Should().BeFalse();
        vm.NewCardTitle.Should().BeEmpty();
        refreshed.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmAddCardAsync_SetsErrorAndKeepsFormOpenOnFailure()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card>(new InvalidOperationException("DB error")));
        var vm = NewVm(mediator: mediator);
        vm.BeginAddCardCommand.Execute(null); // opens the add form
        vm.NewCardTitle = "New card";

        await vm.ConfirmAddCardCommand.ExecuteAsync(null);

        vm.AddCardErrorMessage.Should().Be("DB error");
        vm.IsAddingCard.Should().BeTrue();
    }

    private static LaneViewModel NewVm(
        string name = "Doing",
        IMediator? mediator = null,
        Func<Task>? refreshBoard = null) =>
        new(mediator ?? Substitute.For<IMediator>(), refreshBoard ?? (() => Task.CompletedTask))
        {
            WorkspaceId = Guid.NewGuid(),
            Name = name,
        };
}
