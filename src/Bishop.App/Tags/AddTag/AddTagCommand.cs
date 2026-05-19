using Bishop.Core;
using MediatR;

namespace Bishop.App.Tags.AddTag;

public sealed record AddTagCommand(Guid WorkspaceId, string Name, string? Colour = null) : IRequest<Tag>;
