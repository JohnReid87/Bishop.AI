using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.PushCard;

public sealed record PushCardCommand(Guid CardId) : IRequest<Card>;
