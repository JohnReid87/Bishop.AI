using Bishop.App.Skills;
using Bishop.ViewModels.Cards;
using Microsoft.UI.Xaml;

namespace Bishop.UI.Services;

public interface IDialogService
{
    Task<CardDetailDialogViewModel> ShowCardDetailDialogAsync(
        CardViewModel card, SkillMenuItem[] cardSkills, string workspacePath,
        Guid workspaceId, string? gitHubRepo, XamlRoot xamlRoot);

    Task ShowSettingsDialogAsync(XamlRoot xamlRoot);
}
