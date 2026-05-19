using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Tags.ListTagsByWorkspace;

public sealed class ListTagsByWorkspaceQueryHandler : IRequestHandler<ListTagsByWorkspaceQuery, IReadOnlyList<Tag>>
{
    private readonly BishopDbContext _db;

    public ListTagsByWorkspaceQueryHandler(BishopDbContext db) => _db = db;

    public async Task<IReadOnlyList<Tag>> Handle(ListTagsByWorkspaceQuery request, CancellationToken cancellationToken)
    {
        return await _db.Tags
            .AsNoTracking()
            .Where(t => t.WorkspaceId == request.WorkspaceId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }
}
