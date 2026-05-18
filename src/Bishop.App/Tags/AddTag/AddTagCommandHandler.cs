using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Tags.AddTag;

public sealed class AddTagCommandHandler : IRequestHandler<AddTagCommand, Tag>
{
    private readonly BishopDbContext _db;

    public AddTagCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Tag> Handle(AddTagCommand request, CancellationToken cancellationToken)
    {
        var existing = await _db.Tags
            .FirstOrDefaultAsync(
                t => t.WorkspaceId == request.WorkspaceId && t.Name == request.Name,
                cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Tag '{request.Name}' already exists in this workspace.");

        var tag = new Tag { Id = Guid.NewGuid(), WorkspaceId = request.WorkspaceId, Name = request.Name };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync(cancellationToken);
        return tag;
    }
}
