using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Findings.DismissFinding;

internal sealed class DismissFindingCommandHandler : IRequestHandler<DismissFindingCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public DismissFindingCommandHandler(IDbContextFactory<BishopDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task Handle(DismissFindingCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RebuttalText))
            throw new InvalidOperationException("Rebuttal text is required to dismiss a finding.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var finding = await db.Findings
            .FirstOrDefaultAsync(f => f.Id == request.FindingId, cancellationToken);
        if (finding is null)
            throw new InvalidOperationException($"Finding {request.FindingId} not found.");

        finding.Status = "dismissed";
        finding.RebuttalText = request.RebuttalText.Trim();

        await db.SaveChangesAsync(cancellationToken);
    }
}
