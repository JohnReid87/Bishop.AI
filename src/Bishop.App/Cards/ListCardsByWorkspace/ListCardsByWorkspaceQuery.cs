using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.ListCardsByWorkspace;

public sealed record ListCardsByWorkspaceQuery(
    Guid WorkspaceId,
    string? TagName = null,
    string? LaneName = null,
    int Skip = 0,
    int Take = int.MaxValue
) : IRequest<IReadOnlyList<Card>>;
