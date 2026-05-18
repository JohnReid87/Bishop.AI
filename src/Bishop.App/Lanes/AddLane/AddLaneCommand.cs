using Bishop.Core;
using MediatR;

namespace Bishop.App.Lanes.AddLane;

public sealed record AddLaneCommand(Guid WorkspaceId, string Name) : IRequest<Lane>;
