namespace Bishop.ViewModels.Findings;

internal readonly record struct FindingStatusState(
    string StatusLabel,
    bool IsConvertToCardVisible,
    bool IsDismissEnabled)
{
    internal static FindingStatusState For(string status, int? linkedCardId, bool? linkedCardIsClosed)
    {
        var label = (linkedCardId, linkedCardIsClosed) switch
        {
            ({ } n, false) => $"open #{n}",
            ({ } n, true) => $"done #{n}",
            _ => status switch
            {
                "dismissed" => "dismissed",
                "parked" => "parked",
                "resolved" => "resolved",
                _ => "pending",
            },
        };
        var isConvertVisible = linkedCardId is null && status != "dismissed" && status != "resolved";
        var isDismissEnabled = status != "dismissed" && status != "resolved" && linkedCardId is null;
        return new(label, isConvertVisible, isDismissEnabled);
    }
}
