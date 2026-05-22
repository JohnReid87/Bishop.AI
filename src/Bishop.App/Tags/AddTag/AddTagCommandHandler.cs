using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Tags.AddTag;

public sealed class AddTagCommandHandler : IRequestHandler<AddTagCommand, Tag>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public AddTagCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Tag> Handle(AddTagCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Tags
            .FirstOrDefaultAsync(
                t => t.WorkspaceId == request.WorkspaceId && t.Name == request.Name,
                cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Tag '{request.Name}' already exists in this workspace.");

        var tag = new Tag { Id = Guid.NewGuid(), WorkspaceId = request.WorkspaceId, Name = request.Name, Colour = request.Colour ?? BrandTagPalette.DefaultColour };
        db.Tags.Add(tag);
        await db.SaveChangesAsync(cancellationToken);
        return tag;
    }
}
