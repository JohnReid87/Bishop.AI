using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.ReopenCard;

public sealed record ReopenCardCommand(Guid CardId) : IRequest<Card>;
