using Bishop.App.Skills;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Settings;
using Microsoft.UI.Xaml;

namespace Bishop.UI.Services;

public interface IDialogService
{
    Task<CardDetailDialogViewModel> ShowCardDetailDialogAsync(
        CardViewModel card, SkillMenuItem[] cardSkills, string workspacePath,
        Guid workspaceId, string? gitHubRepo, XamlRoot xamlRoot);

    Task<ImportFromGitHubDialogViewModel> ShowImportFromGitHubDialogAsync(
        Guid workspaceId, string repo, XamlRoot xamlRoot);

    Task<PushLaneToGitHubDialogViewModel> ShowPushLaneToGitHubDialogAsync(
        Guid workspaceId, string laneName, IReadOnlyList<CardViewModel> cards, XamlRoot xamlRoot);

    Task ShowSettingsDialogAsync(XamlRoot xamlRoot);

    // Returns the trimmed repo string the user entered (empty string = unlink),
    // or null if the dialog was cancelled.
    Task<string?> ShowWorkspaceSettingsDialogAsync(string? currentRepo, XamlRoot xamlRoot);
}
