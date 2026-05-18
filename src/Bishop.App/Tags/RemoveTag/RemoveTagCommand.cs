using MediatR;

namespace Bishop.App.Tags.RemoveTag;

public sealed record RemoveTagCommand(Guid WorkspaceId, string Name) : IRequest<Unit>;
