namespace Bishop.ViewModels.Cards;

/// <summary>
/// Presentation-agnostic launcher for <c>CardDetailDialog</c>. Lives in
/// Bishop.ViewModels so VMs can drive the dialog without referencing
/// Microsoft.UI.*; the UI layer implements it by adapting the WinUI 3
/// <c>XamlRoot</c> from the supplied <paramref name="xamlRoot"/> object.
/// Returns <c>true</c> when the user dismissed via the primary action,
/// <c>false</c> for any other close path (X, Escape, secondary).
/// </summary>
public interface ICardDetailDialogService
{
    Task<bool> ShowAsync(
        CardViewModel card,
        string workspacePath,
        Guid workspaceId,
        object xamlRoot);

    Task ShowNotFoundAsync(int cardNumber, object xamlRoot);
}
