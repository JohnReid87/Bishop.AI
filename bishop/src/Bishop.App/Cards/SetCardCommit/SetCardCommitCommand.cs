using Bishop.Core;
using MediatR;

namespace Bishop.App.Cards.SetCardCommit;

public sealed record SetCardCommitCommand(Guid CardId, string Hash, string Branch) : IRequest<Card>;
