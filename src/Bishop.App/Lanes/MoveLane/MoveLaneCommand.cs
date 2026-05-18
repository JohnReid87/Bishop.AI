using Bishop.Core;
using MediatR;

namespace Bishop.App.Lanes.MoveLane;

public sealed record MoveLaneCommand(Guid LaneId, int ToPosition) : IRequest<Lane>;
