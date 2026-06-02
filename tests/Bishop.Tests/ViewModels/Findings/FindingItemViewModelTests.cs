using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Findings.DismissFinding;
using Bishop.App.Findings.GetFindingsBySkillAndProject;
using Bishop.App.Findings.LinkFindingToCard;
using Bishop.Core;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Findings;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Findings;

public class FindingItemViewModelTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private const string WorkspacePath = @"C:\fake\workspace";
    private const string SkillName = "bish-arch";

    private static FindingRecord MakeRecord(
        string status = "pending",
        string? severity = null,
        string? file = null,
        string? symbol = null,
        string? rule = null,
        string? rebuttalText = null,
        int? linkedCardId = null) =>
        new(
            Id: Guid.NewGuid(),
            Title: "Test finding",
            Body: "Something is wrong",
            Severity: severity,
            File: file,
            Symbol: symbol,
            Rule: rule,
            Status: status,
            RebuttalText: rebuttalText,
            LinkedCardId: linkedCardId);

    private static FindingItemViewModel MakeVm(
        FindingRecord? record = null,
        ISender? mediator = null,
        ICardDetailDialogService? dialogService = null) =>
        new(
            record ?? MakeRecord(),
            SkillName,
            WorkspaceId,
            WorkspacePath,
            mediator ?? Substitute.For<ISender>(),
            dialogService ?? Substitute.For<ICardDetailDialogService>());

    // --- Location ---

    [Fact]
    public void Location_NoFileNoSymbol_ReturnsEmpty()
    {
        var vm = MakeVm(MakeRecord(file: null, symbol: null));

        vm.Location.Should().BeEmpty();
    }

    [Fact]
    public void Location_NoFileHasSymbol_ReturnsSymbol()
    {
        var vm = MakeVm(MakeRecord(file: null, symbol: "MyMethod"));

        vm.Location.Should().Be("MyMethod");
    }

    [Fact]
    public void Location_HasFileNoSymbol_ReturnsFile()
    {
        var vm = MakeVm(MakeRecord(file: "src/Foo.cs", symbol: null));

        vm.Location.Should().Be("src/Foo.cs");
    }

    [Fact]
    public void Location_HasFileAndSymbol_ReturnsCombined()
    {
        var vm = MakeVm(MakeRecord(file: "src/Foo.cs", symbol: "Foo.Bar"));

        vm.Location.Should().Be("src/Foo.cs · Foo.Bar");
    }

    // --- SeverityColor ---

    [Theory]
    [InlineData("critical", "#c97a8a")]
    [InlineData("high", "#c97a8a")]
    [InlineData("CRITICAL", "#c97a8a")]
    [InlineData("medium", "#c4a85f")]
    [InlineData("med", "#c4a85f")]
    [InlineData("low", "#5fa89c")]
    [InlineData("info", "#5fa89c")]
    [InlineData("other", "#9aa86a")]
    [InlineData(null, "#9aa86a")]
    public void SeverityColor_ReturnsExpected(string? severity, string expected)
    {
        var vm = MakeVm(MakeRecord(severity: severity));

        vm.SeverityColor.Should().Be(expected);
    }

    // --- HasSeverity ---

    [Fact]
    public void HasSeverity_NullSeverity_ReturnsFalse()
    {
        var vm = MakeVm(MakeRecord(severity: null));

        vm.HasSeverity.Should().BeFalse();
    }

    [Fact]
    public void HasSeverity_WithSeverity_ReturnsTrue()
    {
        var vm = MakeVm(MakeRecord(severity: "high"));

        vm.HasSeverity.Should().BeTrue();
    }

    // --- StatusLabel ---

    [Theory]
    [InlineData("dismissed", "dismissed")]
    [InlineData("parked", "parked")]
    [InlineData("resolved", "resolved")]
    [InlineData("pending", "pending")]
    [InlineData("carded", "pending")]
    public void StatusLabel_ReturnsExpected(string status, string expected)
    {
        var vm = MakeVm(MakeRecord(status: status));

        vm.StatusLabel.Should().Be(expected);
    }

    [Fact]
    public void StatusLabel_WithLinkedCardId_ReturnsCardRef()
    {
        var vm = MakeVm(MakeRecord(status: "carded", linkedCardId: 42));

        vm.StatusLabel.Should().Be("#42");
    }

    // --- Boolean computed properties ---

    [Fact]
    public void IsResolved_TrueWhenStatusIsResolved()
    {
        MakeVm(MakeRecord(status: "resolved")).IsResolved.Should().BeTrue();
        MakeVm(MakeRecord(status: "pending")).IsResolved.Should().BeFalse();
    }

    [Fact]
    public void IsDismissed_TrueWhenStatusIsDismissed()
    {
        MakeVm(MakeRecord(status: "dismissed")).IsDismissed.Should().BeTrue();
        MakeVm(MakeRecord(status: "pending")).IsDismissed.Should().BeFalse();
    }

    [Fact]
    public void HasRebuttal_TrueWhenRebuttalTextIsSet()
    {
        MakeVm(MakeRecord(rebuttalText: "Not an issue")).HasRebuttal.Should().BeTrue();
        MakeVm(MakeRecord(rebuttalText: null)).HasRebuttal.Should().BeFalse();
        MakeVm(MakeRecord(rebuttalText: "   ")).HasRebuttal.Should().BeFalse();
    }

    [Fact]
    public void HasLinkedCard_TrueWhenLinkedCardIdIsSet()
    {
        MakeVm(MakeRecord(linkedCardId: 5)).HasLinkedCard.Should().BeTrue();
        MakeVm(MakeRecord(linkedCardId: null)).HasLinkedCard.Should().BeFalse();
    }

    [Fact]
    public void LinkedCardLabel_ReturnsFormattedStringOrEmpty()
    {
        MakeVm(MakeRecord(linkedCardId: 7)).LinkedCardLabel.Should().Be("Open card #7");
        MakeVm(MakeRecord(linkedCardId: null)).LinkedCardLabel.Should().BeEmpty();
    }

    [Fact]
    public void IsConvertToCardVisible_FalseWhenAlreadyLinked()
    {
        MakeVm(MakeRecord(linkedCardId: 1)).IsConvertToCardVisible.Should().BeFalse();
    }

    [Fact]
    public void IsConvertToCardVisible_FalseWhenDismissed()
    {
        MakeVm(MakeRecord(status: "dismissed")).IsConvertToCardVisible.Should().BeFalse();
    }

    [Fact]
    public void IsConvertToCardVisible_FalseWhenResolved()
    {
        MakeVm(MakeRecord(status: "resolved")).IsConvertToCardVisible.Should().BeFalse();
    }

    [Fact]
    public void IsConvertToCardVisible_TrueWhenPendingWithNoCard()
    {
        MakeVm(MakeRecord(status: "pending", linkedCardId: null)).IsConvertToCardVisible.Should().BeTrue();
    }

    [Fact]
    public void IsDismissEnabled_FalseWhenDismissed()
    {
        MakeVm(MakeRecord(status: "dismissed")).IsDismissEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsDismissEnabled_FalseWhenResolved()
    {
        MakeVm(MakeRecord(status: "resolved")).IsDismissEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsDismissEnabled_FalseWhenLinkedCardPresent()
    {
        MakeVm(MakeRecord(linkedCardId: 3)).IsDismissEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsDismissEnabled_TrueWhenPendingAndNoCard()
    {
        MakeVm(MakeRecord(status: "pending", linkedCardId: null)).IsDismissEnabled.Should().BeTrue();
    }

    // --- DismissAsync ---

    [Fact]
    public async Task DismissAsync_WithValidRebuttal_SendsCommandAndUpdatesState()
    {
        var mediator = Substitute.For<ISender>();
        var vm = MakeVm(MakeRecord(status: "pending"), mediator: mediator);

        await vm.DismissCommand.ExecuteAsync("Not an issue");

        await mediator.Received(1).Send(
            Arg.Is<DismissFindingCommand>(c => c.RebuttalText == "Not an issue"),
            Arg.Any<CancellationToken>());
        vm.Status.Should().Be("dismissed");
        vm.RebuttalText.Should().Be("Not an issue");
        vm.IsDismissed.Should().BeTrue();
    }

    [Fact]
    public async Task DismissAsync_TrimsRebuttalText()
    {
        var mediator = Substitute.For<ISender>();
        var vm = MakeVm(MakeRecord(status: "pending"), mediator: mediator);

        await vm.DismissCommand.ExecuteAsync("  some whitespace  ");

        await mediator.Received(1).Send(
            Arg.Is<DismissFindingCommand>(c => c.RebuttalText == "some whitespace"),
            Arg.Any<CancellationToken>());
        vm.RebuttalText.Should().Be("some whitespace");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DismissAsync_WithBlankRebuttal_DoesNothing(string? rebuttal)
    {
        var mediator = Substitute.For<ISender>();
        var vm = MakeVm(MakeRecord(status: "pending"), mediator: mediator);

        await vm.DismissCommand.ExecuteAsync(rebuttal);

        await mediator.DidNotReceive().Send(Arg.Any<DismissFindingCommand>(), Arg.Any<CancellationToken>());
        vm.Status.Should().Be("pending");
    }

    [Fact]
    public async Task DismissAsync_WhenAlreadyDismissed_DoesNothing()
    {
        var mediator = Substitute.For<ISender>();
        var vm = MakeVm(MakeRecord(status: "dismissed"), mediator: mediator);

        await vm.DismissCommand.ExecuteAsync("another reason");

        await mediator.DidNotReceive().Send(Arg.Any<DismissFindingCommand>(), Arg.Any<CancellationToken>());
    }

    // --- ConvertToCardAsync ---

    [Fact]
    public async Task ConvertToCardAsync_NullXamlRoot_DoesNothing()
    {
        var mediator = Substitute.For<ISender>();
        var vm = MakeVm(mediator: mediator);

        await vm.ConvertToCardCommand.ExecuteAsync(null);

        await mediator.DidNotReceive().Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConvertToCardAsync_WhenNotEnabled_DoesNothing()
    {
        var mediator = Substitute.For<ISender>();
        var vm = MakeVm(MakeRecord(status: "dismissed"), mediator: mediator);

        await vm.ConvertToCardCommand.ExecuteAsync(new object());

        await mediator.DidNotReceive().Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConvertToCardAsync_SavedTrue_LinksCardAndUpdatesState()
    {
        var mediator = Substitute.For<ISender>();
        var dialogService = Substitute.For<ICardDetailDialogService>();

        var card = new Card
        {
            Id = Guid.NewGuid(),
            Number = 99,
            Title = "Test finding",
            Description = "",
            LaneName = "To Do",
        };
        mediator.Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(card);
        dialogService.ShowAsync(Arg.Any<CardViewModel>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<object>())
            .Returns(true);

        var vm = MakeVm(MakeRecord(status: "pending"), mediator: mediator, dialogService: dialogService);

        await vm.ConvertToCardCommand.ExecuteAsync(new object());

        await mediator.Received(1).Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(
            Arg.Is<LinkFindingToCardCommand>(c => c.CardNumber == 99),
            Arg.Any<CancellationToken>());
        vm.LinkedCardId.Should().Be(99);
        vm.Status.Should().Be("carded");
    }

    [Fact]
    public async Task ConvertToCardAsync_SavedFalse_RemovesCardAndNoLink()
    {
        var mediator = Substitute.For<ISender>();
        var dialogService = Substitute.For<ICardDetailDialogService>();

        var cardId = Guid.NewGuid();
        var card = new Card { Id = cardId, Number = 10, Title = "Test finding", Description = "", LaneName = "To Do" };
        mediator.Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>()).Returns(card);
        dialogService.ShowAsync(Arg.Any<CardViewModel>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<object>())
            .Returns(false);

        var vm = MakeVm(MakeRecord(status: "pending"), mediator: mediator, dialogService: dialogService);

        await vm.ConvertToCardCommand.ExecuteAsync(new object());

        await mediator.Received(1).Send(
            Arg.Is<RemoveCardCommand>(c => c.CardId == cardId),
            Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().Send(Arg.Any<LinkFindingToCardCommand>(), Arg.Any<CancellationToken>());
        vm.LinkedCardId.Should().BeNull();
        vm.Status.Should().Be("pending");
    }

    // --- OpenLinkedCardAsync ---

    [Fact]
    public async Task OpenLinkedCardAsync_NullXamlRoot_DoesNothing()
    {
        var mediator = Substitute.For<ISender>();
        var vm = MakeVm(MakeRecord(linkedCardId: 5), mediator: mediator);

        await vm.OpenLinkedCardCommand.ExecuteAsync(null);

        await mediator.DidNotReceive().Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenLinkedCardAsync_NoLinkedCard_DoesNothing()
    {
        var mediator = Substitute.For<ISender>();
        var vm = MakeVm(MakeRecord(linkedCardId: null), mediator: mediator);

        await vm.OpenLinkedCardCommand.ExecuteAsync(new object());

        await mediator.DidNotReceive().Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenLinkedCardAsync_CardFound_ShowsDialog()
    {
        var mediator = Substitute.For<ISender>();
        var dialogService = Substitute.For<ICardDetailDialogService>();

        var card = new Card { Id = Guid.NewGuid(), Number = 5, Title = "Linked", Description = "", LaneName = "To Do" };
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>()).Returns(card);

        var vm = MakeVm(MakeRecord(linkedCardId: 5), mediator: mediator, dialogService: dialogService);
        var root = new object();

        await vm.OpenLinkedCardCommand.ExecuteAsync(root);

        await dialogService.Received(1).ShowAsync(
            Arg.Any<CardViewModel>(), WorkspacePath, WorkspaceId, root);
    }

    [Fact]
    public async Task OpenLinkedCardAsync_CardNotFound_ShowsNotFoundDialog()
    {
        var mediator = Substitute.For<ISender>();
        var dialogService = Substitute.For<ICardDetailDialogService>();

        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns((Card?)null);

        var vm = MakeVm(MakeRecord(linkedCardId: 5), mediator: mediator, dialogService: dialogService);
        var root = new object();

        await vm.OpenLinkedCardCommand.ExecuteAsync(root);

        await dialogService.Received(1).ShowNotFoundAsync(5, root);
    }
}
