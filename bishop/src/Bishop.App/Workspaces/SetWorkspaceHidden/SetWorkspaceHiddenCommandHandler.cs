using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.SetWorkspaceHidden;

internal sealed class SetWorkspaceHiddenCommandHandler : IRequestHandler<SetWorkspaceHiddenCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public SetWorkspaceHiddenCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, TimeProvider timeProvider)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task Handle(SetWorkspaceHiddenCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspace = await db.Workspaces.FindAsync([request.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.Id} not found.");

        workspace.IsHidden = request.Hidden;
        workspace.HiddenAt = request.Hidden ? _timeProvider.GetUtcNow() : null;
        await db.SaveChangesAsync(cancellationToken);
    }
}
