using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.AddCard;

public sealed record AddCardCommand(
    Guid LaneId,
    string Title,
    string Description = "",
    string? TagName = null,
    CardInsertPosition Position = CardInsertPosition.Top) : IRequest<Card>;
