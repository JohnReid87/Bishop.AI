using Bishop.Core;
using MediatR;

namespace Bishop.App.Tags.ListTagsByWorkspace;

public sealed record ListTagsByWorkspaceQuery(Guid WorkspaceId) : IRequest<IReadOnlyList<TagInfo>>;
