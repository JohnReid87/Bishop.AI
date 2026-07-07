using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.UnstarCard;

public sealed record UnstarCardCommand(Guid CardId) : IRequest<Card>;
