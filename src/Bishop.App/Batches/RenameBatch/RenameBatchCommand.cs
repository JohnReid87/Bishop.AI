using Bishop.Core;
using MediatR;

namespace Bishop.App.Batches.RenameBatch;

public sealed record RenameBatchCommand(string Name, string NewName) : IRequest<Batch>;
