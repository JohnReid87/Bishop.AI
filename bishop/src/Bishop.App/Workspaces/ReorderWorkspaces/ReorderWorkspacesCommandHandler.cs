using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.ReorderWorkspaces;

internal sealed class ReorderWorkspacesCommandHandler : IRequestHandler<ReorderWorkspacesCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public ReorderWorkspacesCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task Handle(ReorderWorkspacesCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspaces = await db.Workspaces.ToListAsync(cancellationToken);
        var positionMap = request.OrderedIds
            .Select((id, index) => (id, position: index + 1))
            .ToDictionary(x => x.id, x => x.position);

        foreach (var workspace in workspaces)
        {
            if (positionMap.TryGetValue(workspace.Id, out var position))
                workspace.Position = position;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
