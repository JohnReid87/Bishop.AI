using MediatR;

namespace Bishop.App.Context;

public sealed record PrintContextQuery(string WorkspacePath, string? SectionName = null) : IRequest<string>;
