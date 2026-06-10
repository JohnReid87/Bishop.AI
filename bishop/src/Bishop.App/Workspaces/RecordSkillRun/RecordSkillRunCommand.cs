using MediatR;

namespace Bishop.App.Workspaces.RecordSkillRun;

public sealed record RecordSkillRunCommand(Guid WorkspaceId, string SkillName, string GitSha) : IRequest;
