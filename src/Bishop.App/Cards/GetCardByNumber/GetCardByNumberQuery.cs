using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.GetCardByNumber;

public sealed record GetCardByNumberQuery(int Number, Guid WorkspaceId) : IRequest<Card?>;
