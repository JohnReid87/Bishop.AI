using Bishop.Core;

namespace Bishop.Data;

public static class BatchQueries
{
    public static IQueryable<Batch> ByWorkspace(this IQueryable<Batch> q, Guid workspaceId)
        => q.Where(b => b.WorkspaceId == workspaceId);

    public static IQueryable<Batch> ByName(this IQueryable<Batch> q, string name)
        => q.Where(b => b.Name == name);

    public static IQueryable<Batch> ByName(this IQueryable<Batch> q, Guid workspaceId, string name)
        => q.Where(b => b.WorkspaceId == workspaceId && b.Name == name);
}
