using MediatR;

namespace Bishop.App.Cards.RecordCardSuccess;

/// <summary>
/// Atomic post-success mutations applied to a card after an auto-run completes
/// cleanly: token totals, commit hash, success timestamp, description append,
/// and the move into the Done lane — all under one <c>SaveChangesAsync</c>.
/// </summary>
public sealed record RecordCardSuccessCommand(
    Guid CardId,
    string CommitHash,
    string BranchName,
    int InputTokens,
    int OutputTokens,
    int CacheCreationTokens,
    int CacheReadTokens,
    decimal CostUsd,
    string? AppendDescription) : IRequest;
