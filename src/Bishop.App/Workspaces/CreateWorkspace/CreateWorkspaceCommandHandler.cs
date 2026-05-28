using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Bishop.App.Workspaces.CreateWorkspace;

public sealed class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, Workspace>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public CreateWorkspaceCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Workspace> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        if (!Path.IsPathRooted(request.Path))
            throw new ArgumentException($"Workspace path must be an absolute path: '{request.Path}'.");

        if (request.Path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s == ".."))
            throw new ArgumentException($"Workspace path must not contain traversal segments: '{request.Path}'.");

        var canonicalPath = Path.GetFullPath(request.Path);

        if (request.InitGit)
        {
            Directory.CreateDirectory(canonicalPath);
            var psi = new ProcessStartInfo("git")
            {
                WorkingDirectory = canonicalPath,
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("init");
            using var proc = Process.Start(psi);
            if (proc is not null)
                await proc.WaitForExitAsync(cancellationToken);
        }

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
