using Bishop.App.Cards.PushCard;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.PushLane;

internal sealed class PushLaneCommandHandler : IRequestHandler<PushLaneCommand, PushLaneResult>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly ISender _sender;

    public PushLaneCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, ISender sender)
    {
        _dbFactory = dbFactory;
        _sender = sender;
    }

    public async Task<PushLaneResult> Handle(PushLaneCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var workspace = await db.Workspaces.FindAsync([request.WorkspaceId], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.WorkspaceId} not found.");

        if (workspace.GitHubRepo is null)
            throw new InvalidOperationException(
                $"Workspace '{workspace.Name}' has no GitHub repo configured. Run: bishop workspace set-github <owner/repo>");

        if (!SystemLaneNames.All.Contains(request.LaneName))
            throw new InvalidOperationException($"Lane '{request.LaneName}' not found.");

        var cards = await db.Cards
            .Where(c => c.WorkspaceId == request.WorkspaceId && c.LaneName == request.LaneName)
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);

        var skippedCount = cards.Count(c => c.GitHubIssueNumber.HasValue);
        var toPush = cards.Where(c => !c.GitHubIssueNumber.HasValue).ToList();

        if (request.DryRun)
            return new PushLaneResult(toPush, skippedCount, []);

        var pushed = new List<Card>();
        var failed = new List<PushLaneFailure>();

        foreach (var card in toPush)
        {
            try
            {
                var result = await _sender.Send(new PushCardCommand(card.Id), cancellationToken);
                pushed.Add(result);
            }
            catch (Exception ex)
            {
                failed.Add(new PushLaneFailure(card.Number, ex.Message));
            }
        }

        return new PushLaneResult(pushed, skippedCount, failed);
    }
}
