using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.UpdateCard;

public sealed record UpdateCardCommand(
    Guid CardId,
    string? Title,
    string? Description,
    bool UpdateTag,
    string? TagName,
    string? AppendDescription = null,
    string? ToLaneName = null,
    string? CommitHash = null,
    string? CommitBranchName = null) : IRequest<Card>;
