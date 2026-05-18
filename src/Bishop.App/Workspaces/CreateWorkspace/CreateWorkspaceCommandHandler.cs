using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Bishop.App.Workspaces.CreateWorkspace;

public sealed class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, Workspace>
{
    private readonly BishopDbContext _db;

    public CreateWorkspaceCommandHandler(BishopDbContext db) => _db = db;

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

        var position = await _db.Workspaces.CountAsync(cancellationToken) + 1;
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Path = request.Path,
            Position = position,
        };
        _db.Workspaces.Add(workspace);

        string[] laneNames = ["To Do", "Doing", "Done"];
        for (var i = 0; i < laneNames.Length; i++)
        {
            _db.Lanes.Add(new Lane
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                Name = laneNames[i],
                Position = i + 1,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return workspace;
    }
}
