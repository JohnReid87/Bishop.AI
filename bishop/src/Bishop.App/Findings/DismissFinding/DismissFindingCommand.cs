using MediatR;

namespace Bishop.App.Findings.DismissFinding;

internal sealed record DismissFindingCommand(Guid FindingId, string RebuttalText) : IRequest;
