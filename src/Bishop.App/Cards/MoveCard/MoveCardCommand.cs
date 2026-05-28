using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.MoveCard;

public sealed record MoveCardCommand(
    Guid CardId,
    string ToLaneName,
    int ToPosition,
    bool KeepOpen = false,
    string? ExpectedSourceLaneName = null) : IRequest<Card>;
