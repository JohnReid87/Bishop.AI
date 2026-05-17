using Bishop.Data;
using MediatR;

namespace Bishop.App.Workspaces.DeleteWorkspace;

public sealed class DeleteWorkspaceCommandHandler : IRequestHandler<DeleteWorkspaceCommand, Unit>
{
    private readonly BishopDbContext _db;

    public DeleteWorkspaceCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _db.Workspaces.FindAsync([request.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.Id} not found.");

        _db.Workspaces.Remove(workspace);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
