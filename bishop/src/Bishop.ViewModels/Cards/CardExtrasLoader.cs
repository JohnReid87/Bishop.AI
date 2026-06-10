using System.IO;
using Bishop.App.Cards.GetCard;
using Bishop.App.Git;
using Bishop.App.Git.GetCardCommit;
using Bishop.ViewModels.Errors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bishop.ViewModels.Cards;

internal sealed class CardExtrasLoader(ISender mediator, ILogger logger, IErrorBus errorBus)
{
    internal async Task<CardExtrasResult?> LoadAsync(Guid cardId, string workspacePath, int number)
    {
        try
        {
            var card = await mediator.Send(new GetCardQuery(cardId));
            if (card is null) return null;
            var transcriptPath = card.LastAutoRunFailedAt.HasValue
                ? FindLatestTranscript(workspacePath, number)
                : null;
            var commitResult = await mediator.Send(new GetCardCommitQuery(number, workspacePath));
            var commit = commitResult is GetCardCommitResult.Found found ? found.Commit : (CommitInfo?)null;
            return new CardExtrasResult(card.TotalInputTokens, card.TotalOutputTokens, card.ClaudeRunCount, transcriptPath, commit);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load card extras for card {CardId}", cardId);
            errorBus.Report(ex);
            return null;
        }
    }

    private static string? FindLatestTranscript(string workspacePath, int cardNumber)
    {
        var dir = Path.Combine(workspacePath, ".bishop", "runs");
        if (!Directory.Exists(dir)) return null;
        return Directory.GetFiles(dir, $"{cardNumber}-*.jsonl")
            .OrderDescending()
            .FirstOrDefault();
    }
}

internal sealed record CardExtrasResult(
    int InputTokens,
    int OutputTokens,
    int RunCount,
    string? FailedTranscriptPath,
    CommitInfo? Commit);
