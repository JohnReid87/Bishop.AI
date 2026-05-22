using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Tags.ListTagsByWorkspace;

public sealed class ListTagsByWorkspaceQueryHandler : IRequestHandler<ListTagsByWorkspaceQuery, IReadOnlyList<Tag>>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public ListTagsByWorkspaceQueryHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<Tag>> Handle(ListTagsByWorkspaceQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tags
            .AsNoTracking()
            .Where(t => t.WorkspaceId == request.WorkspaceId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }
}
