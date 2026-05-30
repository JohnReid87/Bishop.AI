using Bishop.App.Skills;
using MediatR;

namespace Bishop.App.Batches.CreateBatch;

public sealed record CreateBatchCommand(
    Guid WorkspaceId,
    string WorkspacePath,
    string Name,
    string BranchName,
    string? BaseBranch,
    string WorktreePath,
    int[] CardNumbers,
    string? TagName,
    string? LaneName,
    string Model = SkillModelOptions.DefaultModelId) : IRequest<CreateBatchResult>;
