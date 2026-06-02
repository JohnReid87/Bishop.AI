using MediatR;

namespace Bishop.App.Findings.LinkFindingToCard;

internal sealed record LinkFindingToCardCommand(Guid FindingId, int CardNumber) : IRequest;
