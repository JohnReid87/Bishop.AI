using Bishop.App.Git;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Git.GetCardCommit;

internal sealed class GetCardCommitQueryHandler : IRequestHandler<GetCardCommitQuery, GetCardCommitResult>
{
    private readonly IGitCli _git;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public GetCardCommitQueryHandler(IGitCli git, IDbContextFactory<BishopDbContext> dbFactory)
    {
        _git = git;
        _dbFactory = dbFactory;
    }

    public async Task<GetCardCommitResult> Handle(GetCardCommitQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(
                c => c.Number == request.CardNumber && c.Workspace.Path == request.WorkspacePath,
                cancellationToken);

        if (card?.CommitHash is { } hash)
            return await _git.GetCommitByHashAsync(hash, request.WorkspacePath, cancellationToken);

        return await _git.GetCardCommitAsync(request.CardNumber, request.WorkspacePath, cancellationToken);
    }
}
