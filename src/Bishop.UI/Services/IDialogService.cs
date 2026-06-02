using Bishop.App.Skills;
using Bishop.ViewModels.Cards;
using Microsoft.UI.Xaml;

namespace Bishop.UI.Services;

public interface IDialogService
{
    Task<CardDetailDialogViewModel> ShowCardDetailDialogAsync(
        CardViewModel card, SkillMenuItem[] cardSkills, string workspacePath,
        Guid workspaceId, XamlRoot xamlRoot);

    Task ShowSettingsDialogAsync(XamlRoot xamlRoot);
}
