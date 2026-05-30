using Bishop.App.Services.GitHub;
using Bishop.App.Services.Settings;
using Bishop.App.Skills;
using Bishop.UI.Views.Cards;
using Bishop.UI.Views.GitHub;
using Bishop.UI.Views.Settings;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Settings;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Bishop.UI.Services;

public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;

    public DialogService(IServiceProvider services) => _services = services;

    public async Task<CardDetailDialogViewModel> ShowCardDetailDialogAsync(
        CardViewModel card, SkillMenuItem[] cardSkills, string workspacePath,
        Guid workspaceId, string? gitHubRepo, XamlRoot xamlRoot)
    {
        var mediator = _services.GetRequiredService<ISender>();
        var appSettings = _services.GetRequiredService<IAppSettings>();
        var logger = _services.GetRequiredService<ILogger<CardDetailDialogViewModel>>();
        var errorBus = _services.GetRequiredService<IErrorBus>();
        var vm = new CardDetailDialogViewModel(card, cardSkills, workspaceId, gitHubRepo, mediator, appSettings, workspacePath, logger, errorBus);
        var dialog = new CardDetailDialog(vm) { XamlRoot = xamlRoot };
        await dialog.ShowAsync();
        return vm;
    }

    public async Task<ImportFromGitHubDialogViewModel> ShowImportFromGitHubDialogAsync(
        Guid workspaceId, string repo, XamlRoot xamlRoot)
    {
        var mediator = _services.GetRequiredService<ISender>();
        var ghCli = _services.GetRequiredService<IGhCli>();
        var vm = new ImportFromGitHubDialogViewModel(workspaceId, repo, mediator, ghCli);
        var dialog = new ImportFromGitHubDialog(vm) { XamlRoot = xamlRoot };
        await dialog.ShowAsync();
        return vm;
    }

    public async Task<PushLaneToGitHubDialogViewModel> ShowPushLaneToGitHubDialogAsync(
        Guid workspaceId, string laneName, IReadOnlyList<CardViewModel> cards, XamlRoot xamlRoot)
    {
        var mediator = _services.GetRequiredService<ISender>();
        var vm = new PushLaneToGitHubDialogViewModel(cards, workspaceId, laneName, mediator);
        var dialog = new PushLaneToGitHubDialog(vm) { XamlRoot = xamlRoot };
        await dialog.ShowAsync();
        return vm;
    }

    public async Task ShowSettingsDialogAsync(XamlRoot xamlRoot)
    {
        var vm = _services.GetRequiredService<SettingsDialogViewModel>();
        var dialog = new SettingsDialog(vm) { XamlRoot = xamlRoot };
        await dialog.ShowAsync();
    }
}
