using Bishop.App.Services.Settings;
using Bishop.ViewModels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Bishop.UI.Views;

namespace Bishop.UI.Services;

public sealed class CardDetailDialogService : ICardDetailDialogService
{
    private readonly IServiceProvider _services;

    public CardDetailDialogService(IServiceProvider services) => _services = services;

    public async Task<bool> ShowAsync(
        CardViewModel card,
        string workspacePath,
        Guid workspaceId,
        string? gitHubRepo,
        object xamlRoot)
    {
        if (xamlRoot is not XamlRoot root)
            throw new ArgumentException("xamlRoot must be a WinUI XamlRoot.", nameof(xamlRoot));

        var mediator = _services.GetRequiredService<ISender>();
        var appSettings = _services.GetRequiredService<IAppSettings>();
        var logger = _services.GetRequiredService<ILogger<CardDetailDialogViewModel>>();
        var errorBus = _services.GetRequiredService<IErrorBus>();

        var vm = new CardDetailDialogViewModel(
            card, cardSkills: [], workspaceId, gitHubRepo, mediator, appSettings, workspacePath, logger, errorBus);
        var dialog = new CardDetailDialog(vm) { XamlRoot = root };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
