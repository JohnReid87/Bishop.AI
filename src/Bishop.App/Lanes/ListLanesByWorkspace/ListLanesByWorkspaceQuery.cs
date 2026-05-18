using Bishop.Core;
using MediatR;

namespace Bishop.App.Lanes.ListLanesByWorkspace;

public sealed record ListLanesByWorkspaceQuery(Guid WorkspaceId) : IRequest<IReadOnlyList<Lane>>;
