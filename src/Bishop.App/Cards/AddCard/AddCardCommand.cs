using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.AddCard;

public sealed record AddCardCommand(
    Guid WorkspaceId,
    string LaneName,
    string Title,
    string Description = "",
    string? TagName = null,
    CardInsertPosition Position = CardInsertPosition.Top) : IRequest<Card>;
