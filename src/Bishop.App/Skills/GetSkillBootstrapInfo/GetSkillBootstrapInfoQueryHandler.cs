using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Skills.GetSkillBootstrapInfo;

public sealed class GetSkillBootstrapInfoQueryHandler : IRequestHandler<GetSkillBootstrapInfoQuery, SkillBootstrapInfo>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IMediator _mediator;

    public GetSkillBootstrapInfoQueryHandler(IDbContextFactory<BishopDbContext> dbFactory, IMediator mediator)
    {
        _dbFactory = dbFactory;
        _mediator = mediator;
    }

    public async Task<SkillBootstrapInfo> Handle(GetSkillBootstrapInfoQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspace = await db.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workspace '{request.WorkspaceId}' not found.");

        var tags = await _mediator.Send(new ListTagsByWorkspaceQuery(workspace.Id), cancellationToken);
        var lanes = await _mediator.Send(new ListLanesByWorkspaceQuery(workspace.Id), cancellationToken);

        return new SkillBootstrapInfo(
            workspace.Name,
            workspace.Path,
            workspace.GitHubRepo,
            tags,
            lanes);
    }
}
