using Bishop.Core;

namespace Bishop.App.Cards.PushLane;

public sealed record PushLaneResult(
    IReadOnlyList<Card> Pushed,
    int SkippedAlreadyLinked,
    IReadOnlyList<PushLaneFailure> Failed);

public sealed record PushLaneFailure(int CardNumber, string Error);
