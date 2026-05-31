using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Findings.LinkFindingToCard;

public sealed class LinkFindingToCardCommandHandler : IRequestHandler<LinkFindingToCardCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public LinkFindingToCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task Handle(LinkFindingToCardCommand request, CancellationToken cancellationToken)
    {
        if (request.CardNumber <= 0)
            throw new InvalidOperationException("Card number must be positive.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var finding = await db.Findings
            .FirstOrDefaultAsync(f => f.Id == request.FindingId, cancellationToken);
        if (finding is null)
            throw new InvalidOperationException($"Finding {request.FindingId} not found.");

        finding.Status = "carded";
        finding.LinkedCardId = request.CardNumber;

        await db.SaveChangesAsync(cancellationToken);
    }
}
