using MediatR;

namespace Bishop.App.Batches.RescueBatch;

public sealed record RescueBatchCommand(string Name, bool ConfirmReset) : IRequest<RescueBatchResult>;
