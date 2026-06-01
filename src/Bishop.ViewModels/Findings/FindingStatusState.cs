namespace Bishop.ViewModels.Findings;

internal readonly record struct FindingStatusState(
    string StatusLabel,
    bool IsConvertToCardVisible,
    bool IsDismissEnabled)
{
    internal static FindingStatusState For(string status, int? linkedCardId)
    {
        var label = status switch
        {
            "dismissed" => "dismissed",
            "parked" => "parked",
            "resolved" => "resolved",
            _ when linkedCardId is { } n => $"#{n}",
            _ => "pending",
        };
        var isConvertVisible = linkedCardId is null && status != "dismissed" && status != "resolved";
        var isDismissEnabled = status != "dismissed" && status != "resolved" && linkedCardId is null;
        return new(label, isConvertVisible, isDismissEnabled);
    }
}
