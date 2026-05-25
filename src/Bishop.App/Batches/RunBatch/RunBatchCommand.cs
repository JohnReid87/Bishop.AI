using Bishop.App.Skills;
using MediatR;

namespace Bishop.App.Batches.RunBatch;

public sealed record RunBatchCommand(string Name, bool Resume, string Model = SkillModelOptions.DefaultModelId) : IRequest<RunBatchResult>;
