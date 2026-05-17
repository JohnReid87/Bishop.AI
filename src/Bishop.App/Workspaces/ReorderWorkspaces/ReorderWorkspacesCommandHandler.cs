using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.ReorderWorkspaces;

public sealed class ReorderWorkspacesCommandHandler : IRequestHandler<ReorderWorkspacesCommand, Unit>
{
    private readonly BishopDbContext _db;

    public ReorderWorkspacesCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Unit> Handle(ReorderWorkspacesCommand request, CancellationToken cancellationToken)
    {
        var workspaces = await _db.Workspaces.ToListAsync(cancellationToken);
        var positionMap = request.OrderedIds
            .Select((id, index) => (id, position: index + 1))
            .ToDictionary(x => x.id, x => x.position);

        foreach (var workspace in workspaces)
        {
            if (positionMap.TryGetValue(workspace.Id, out var position))
                workspace.Position = position;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
