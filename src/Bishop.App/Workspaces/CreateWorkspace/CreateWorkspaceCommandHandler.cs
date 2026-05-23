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
        if (request.InitGit)
        {
            Directory.CreateDirectory(request.Path);
            var psi = new ProcessStartInfo("git", "init")
            {
                WorkingDirectory = request.Path,
                CreateNoWindow = true,
                UseShellExecute = false,
            };
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
            Path = request.Path,
            Position = position,
        };
        db.Workspaces.Add(workspace);

        await db.SaveChangesAsync(cancellationToken);
        return workspace;
    }
}
