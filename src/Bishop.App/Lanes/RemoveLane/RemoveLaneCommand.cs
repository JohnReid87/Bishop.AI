using MediatR;

namespace Bishop.App.Lanes.RemoveLane;

public sealed record RemoveLaneCommand(Guid LaneId) : IRequest<Unit>;
