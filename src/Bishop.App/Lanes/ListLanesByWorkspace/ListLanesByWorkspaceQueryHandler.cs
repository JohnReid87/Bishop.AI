using Bishop.Core;
using MediatR;

namespace Bishop.App.Lanes.ListLanesByWorkspace;

internal sealed class ListLanesByWorkspaceQueryHandler : IRequestHandler<ListLanesByWorkspaceQuery, IReadOnlyList<LaneInfo>>
{
    private static readonly IReadOnlyList<LaneInfo> Lanes =
        SystemLaneNames.All.Select((name, i) => new LaneInfo(name, i + 1)).ToList();

    public Task<IReadOnlyList<LaneInfo>> Handle(ListLanesByWorkspaceQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Lanes);
}
