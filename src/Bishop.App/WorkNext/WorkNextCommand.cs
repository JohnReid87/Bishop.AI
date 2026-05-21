using MediatR;

namespace Bishop.App.WorkNext;

public sealed record WorkNextCommand(
    Guid WorkspaceId,
    string WorkspacePath,
    string? Tag,
    int MaxIterations,
    string? Model = null) : IRequest<WorkNextResult>;
