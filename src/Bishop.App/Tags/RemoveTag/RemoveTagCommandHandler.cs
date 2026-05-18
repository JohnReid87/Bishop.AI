using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Tags.RemoveTag;

public sealed class RemoveTagCommandHandler : IRequestHandler<RemoveTagCommand, Unit>
{
    private readonly BishopDbContext _db;

    public RemoveTagCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Unit> Handle(RemoveTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await _db.Tags
            .FirstOrDefaultAsync(
                t => t.WorkspaceId == request.WorkspaceId && t.Name == request.Name,
                cancellationToken)
            ?? throw new InvalidOperationException($"Tag '{request.Name}' not found in this workspace.");

        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
