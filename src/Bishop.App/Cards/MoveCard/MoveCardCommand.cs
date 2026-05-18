using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.MoveCard;

public sealed record MoveCardCommand(Guid CardId, Guid ToLaneId, int ToPosition) : IRequest<Card>;
