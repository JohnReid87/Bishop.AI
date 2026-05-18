using Bishop.UI.ViewModels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class CardDetailDialog : ContentDialog
{
    public CardDetailDialogViewModel ViewModel { get; }

    public CardDetailDialog(CardViewModel card)
    {
        var mediator = App.Services.GetRequiredService<IMediator>();
        ViewModel = new CardDetailDialogViewModel(card, mediator);
        InitializeComponent();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CardDetailDialogViewModel.Deleted) && ViewModel.Deleted)
                Hide();
        };
    }
}
