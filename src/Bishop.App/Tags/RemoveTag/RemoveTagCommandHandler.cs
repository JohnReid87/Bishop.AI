using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Tags.RemoveTag;

public sealed class RemoveTagCommandHandler : IRequestHandler<RemoveTagCommand, Unit>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RemoveTagCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Unit> Handle(RemoveTagCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var tag = await db.Tags
            .FirstOrDefaultAsync(
                t => t.WorkspaceId == request.WorkspaceId && t.Name == request.Name,
                cancellationToken)
            ?? throw new InvalidOperationException($"Tag '{request.Name}' not found in this workspace.");

        db.Tags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
