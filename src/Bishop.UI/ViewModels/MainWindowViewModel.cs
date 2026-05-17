using CommunityToolkit.Mvvm.ComponentModel;
using MediatR;

namespace Bishop.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    public MainWindowViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }
}
