using MediatR;

namespace Bishop.App.Cards.RemoveCard;

public sealed record RemoveCardCommand(Guid CardId) : IRequest<Unit>;
