using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.InitWorkspace;

public sealed class InitWorkspaceCommandHandler : IRequestHandler<InitWorkspaceCommand, InitWorkspaceResult>
{
    private static readonly string[] DefaultLaneNames = ["To Do", "Doing", "Done"];

    private readonly BishopDbContext _db;

    public InitWorkspaceCommandHandler(BishopDbContext db) => _db = db;

    public async Task<InitWorkspaceResult> Handle(InitWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var normalizedPath = Path.GetFullPath(request.Path);

        var allWorkspaces = await _db.Workspaces
            .Include(w => w.Lanes)
            .ToListAsync(cancellationToken);

        var existing = allWorkspaces.FirstOrDefault(w =>
            string.Equals(Path.GetFullPath(w.Path), normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            var name = request.Name ?? new DirectoryInfo(normalizedPath).Name;
            var workspace = new Workspace
            {
                Id = Guid.NewGuid(),
                Name = name,
                Path = normalizedPath,
                Position = allWorkspaces.Count + 1,
            };
            _db.Workspaces.Add(workspace);

            for (var i = 0; i < DefaultLaneNames.Length; i++)
            {
                _db.Lanes.Add(new Lane
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspace.Id,
                    Name = DefaultLaneNames[i],
                    Position = i + 1,
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            return new InitWorkspaceResult(workspace, Created: true, LanesAdded: DefaultLaneNames);
        }

        var existingLaneNames = existing.Lanes
            .Select(l => l.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = DefaultLaneNames.Where(n => !existingLaneNames.Contains(n)).ToList();
        if (missing.Count == 0)
            return new InitWorkspaceResult(existing, Created: false, LanesAdded: []);

        var nextPosition = existing.Lanes.Count > 0
            ? existing.Lanes.Max(l => l.Position) + 1
            : 1;

        foreach (var laneName in missing)
        {
            _db.Lanes.Add(new Lane
            {
                Id = Guid.NewGuid(),
                WorkspaceId = existing.Id,
                Name = laneName,
                Position = nextPosition++,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new InitWorkspaceResult(existing, Created: false, LanesAdded: missing);
    }
}
