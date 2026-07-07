using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.StarCard;

public sealed record StarCardCommand(Guid CardId) : IRequest<Card>;
