using Bishop.App.Services.Terminal;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.CreateWorkspace;

internal sealed class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, Workspace>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IWorkspaceBootstrapper _bootstrapper;

    public CreateWorkspaceCommandHandler(
        IDbContextFactory<BishopDbContext> dbFactory,
        IWorkspaceBootstrapper bootstrapper)
    {
        _dbFactory = dbFactory;
        _bootstrapper = bootstrapper;
    }

    public async Task<Workspace> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        if (!Path.IsPathRooted(request.Path))
            throw new ArgumentException($"Workspace path must be an absolute path: '{request.Path}'.");

        if (request.Path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s == ".."))
            throw new ArgumentException($"Workspace path must not contain traversal segments: '{request.Path}'.");

        var canonicalPath = Path.GetFullPath(request.Path);

        if (request.InitGit)
            await _bootstrapper.EnsureBootstrappedAsync(canonicalPath, cancellationToken);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var position = await db.Workspaces.CountAsync(cancellationToken) + 1;
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Path = canonicalPath,
            Position = position,
        };
        db.Workspaces.Add(workspace);

        await db.SaveChangesAsync(cancellationToken);
        return workspace;
    }
}
