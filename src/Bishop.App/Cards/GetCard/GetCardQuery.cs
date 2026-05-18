using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.GetCard;

public sealed record GetCardQuery(Guid CardId) : IRequest<Card?>;
