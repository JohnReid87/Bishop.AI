using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.CloseCard;

public sealed record CloseCardCommand(Guid CardId) : IRequest<Card>;
