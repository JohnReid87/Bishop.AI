using MediatR;

namespace Bishop.App.Batches.RunBatch;

public sealed record RunBatchCommand(string Name, bool Resume, string? Model = null, bool AllowExternalContent = false) : IRequest<RunBatchResult>;
