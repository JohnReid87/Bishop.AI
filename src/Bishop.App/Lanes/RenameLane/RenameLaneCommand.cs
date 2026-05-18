using Bishop.Core;
using MediatR;

namespace Bishop.App.Lanes.RenameLane;

public sealed record RenameLaneCommand(Guid LaneId, string NewName) : IRequest<Lane>;
