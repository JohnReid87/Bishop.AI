using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.ListCardsByWorkspace;

public sealed record ListCardsByWorkspaceQuery(Guid WorkspaceId) : IRequest<IReadOnlyList<Card>>;
