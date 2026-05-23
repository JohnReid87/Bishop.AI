using MediatR;

namespace Bishop.App.Cards.PushLane;

public sealed record PushLaneCommand(
    Guid WorkspaceId,
    string LaneName,
    bool DryRun = false) : IRequest<PushLaneResult>;
