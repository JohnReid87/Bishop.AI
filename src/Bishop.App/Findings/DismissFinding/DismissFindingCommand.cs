using MediatR;

namespace Bishop.App.Findings.DismissFinding;

public sealed record DismissFindingCommand(Guid FindingId, string RebuttalText) : IRequest;
