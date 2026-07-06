using MediatR;

namespace Bishop.App.Findings.RecordFindings;

internal sealed record RecordFindingsCommand(
    Guid WorkspaceId,
    string WorkspacePath,
    string SkillName,
    string FindingsJson,
    string GitSha,
    Guid? BatchId = null) : IRequest<RecordFindingsResult>;
