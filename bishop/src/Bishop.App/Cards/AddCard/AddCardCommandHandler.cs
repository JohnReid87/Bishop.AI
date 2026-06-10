using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.AddCard;

internal sealed class AddCardCommandHandler : IRequestHandler<AddCardCommand, Card>
{
    private const int MaxNumberMintRetries = 5;

    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public AddCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Card> Handle(AddCardCommand request, CancellationToken cancellationToken)
    {
        if (!SystemLaneNames.All.Contains(request.LaneName))
            throw new InvalidOperationException($"Lane '{request.LaneName}' is not a system lane.");

        string? tagName = null;
        if (!string.IsNullOrEmpty(request.TagName))
        {
            if (!BrandTagPalette.DefaultColours.ContainsKey(request.TagName))
                throw new InvalidOperationException($"Tag '{request.TagName}' is not a known tag.");
            tagName = request.TagName;
        }

        DbUpdateException? lastConflict = null;
        for (var attempt = 0; attempt < MaxNumberMintRetries; attempt++)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
                return await InsertCardAsync(db, request, tagName, cancellationToken);
            }
            catch (DbUpdateException ex) when (IsCardNumberConflict(ex))
            {
                lastConflict = ex;
            }
        }

        throw lastConflict!;
    }

    private static async Task<Card> InsertCardAsync(
        BishopDbContext db,
        AddCardCommand request,
        string? tagName,
        CancellationToken cancellationToken)
    {
        var workspace = await db.Workspaces.FindAsync([request.WorkspaceId], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.WorkspaceId} not found.");

        int newPosition;
        if (request.Position == CardInsertPosition.Bottom)
        {
            var maxPosition = await db.Cards
                .Where(c => c.WorkspaceId == workspace.Id && c.LaneName == request.LaneName)
                .MaxAsync(c => (int?)c.Position, cancellationToken);
            newPosition = (maxPosition ?? 0) + 1;
        }
        else
        {
            var existing = await db.Cards
                .Where(c => c.WorkspaceId == workspace.Id && c.LaneName == request.LaneName)
                .ToListAsync(cancellationToken);
            foreach (var c in existing)
                c.Position++;
            newPosition = 1;
        }

        var number = workspace.NextCardNumber++;

        var card = new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            LaneName = request.LaneName,
            TagName = tagName,
            Title = request.Title,
            Description = request.Description,
            Number = number,
            Position = newPosition,
        };
        db.Cards.Add(card);

        await db.SaveChangesAsync(cancellationToken);
        return card;
    }

    private static bool IsCardNumberConflict(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true &&
        ex.InnerException.Message.Contains("Cards.Number", StringComparison.OrdinalIgnoreCase);
}
