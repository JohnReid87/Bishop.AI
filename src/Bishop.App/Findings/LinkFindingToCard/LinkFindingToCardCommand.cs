using MediatR;

namespace Bishop.App.Findings.LinkFindingToCard;

public sealed record LinkFindingToCardCommand(Guid FindingId, int CardNumber) : IRequest;
