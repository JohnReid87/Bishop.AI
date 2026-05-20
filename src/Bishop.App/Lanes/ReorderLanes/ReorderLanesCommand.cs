using MediatR;

namespace Bishop.App.Lanes.ReorderLanes;

public sealed record ReorderLanesCommand(Guid WorkspaceId, IReadOnlyList<Guid> OrderedIds) : IRequest<Unit>;
