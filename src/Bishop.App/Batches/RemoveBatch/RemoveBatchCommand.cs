using MediatR;

namespace Bishop.App.Batches.RemoveBatch;

public sealed record RemoveBatchCommand(string Name) : IRequest;
