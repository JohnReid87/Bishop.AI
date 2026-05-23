using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.Cli;

public sealed class CardResolverTests
{
    private static Workspace MakeWorkspace(string name = "test-ws") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Path = @"C:\test"
    };

    private static Card MakeCard(Guid workspaceId, int number = 1) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = workspaceId,
        Number = number,
        Title = $"Card {number}",
        LaneName = "To Do"
    };

    private static IMediator BuildMediator(
        Workspace ws,
        Card? cardByNumber = null,
        IReadOnlyList<Card>? allCards = null)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(cardByNumber);
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(allCards ?? (IReadOnlyList<Card>)[]);
        return mediator;
    }

    [Fact]
    public async Task ResolveAsync_ByNumber_ReturnsMatchingCard()
    {
        var ws = MakeWorkspace();
        var card = MakeCard(ws.Id, number: 42);
        var resolver = new CardResolver(BuildMediator(ws, cardByNumber: card));

        var result = await resolver.ResolveAsync(ws.Name, "42");

        result.Should().NotBeNull();
        result!.Value.cardId.Should().Be(card.Id);
        result.Value.cardNumber.Should().Be(42);
        result.Value.ws.Id.Should().Be(ws.Id);
    }

    [Fact]
    public async Task ResolveAsync_ByShortUuidPrefix_ReturnsMatchingCard()
    {
        var ws = MakeWorkspace();
        var card = MakeCard(ws.Id, number: 7);
        var prefix = card.Id.ToString("N")[..8];
        var resolver = new CardResolver(BuildMediator(ws, allCards: [card]));

        var result = await resolver.ResolveAsync(ws.Name, prefix);

        result.Should().NotBeNull();
        result!.Value.cardId.Should().Be(card.Id);
        result.Value.cardNumber.Should().Be(7);
    }

    [Fact]
    public async Task ResolveAsync_NoMatch_ThrowsInvalidOperationException()
    {
        var ws = MakeWorkspace();
        var resolver = new CardResolver(BuildMediator(ws, allCards: []));

        var act = () => resolver.ResolveAsync(ws.Name, "deadbeef");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deadbeef*");
    }
}
