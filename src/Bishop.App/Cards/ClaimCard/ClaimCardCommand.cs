using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.ClaimCard;

public sealed record ClaimCardCommand(
    Guid WorkspaceId,
    string SourceLaneName,
    string? TagName = null) : IRequest<Card?>;
