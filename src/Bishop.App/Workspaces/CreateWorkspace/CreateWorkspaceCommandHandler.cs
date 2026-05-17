using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.CreateWorkspace;

public sealed class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, Workspace>
{
    private readonly BishopDbContext _db;

    public CreateWorkspaceCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Workspace> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var position = await _db.Workspaces.CountAsync(cancellationToken) + 1;
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Path = request.Path,
            Position = position,
        };
        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync(cancellationToken);
        return workspace;
    }
}
