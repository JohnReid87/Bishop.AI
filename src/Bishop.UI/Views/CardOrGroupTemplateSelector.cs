using Bishop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed class CardOrGroupTemplateSelector : DataTemplateSelector
{
    public DataTemplate? CardTemplate { get; set; }
    public DataTemplate? GroupTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item) =>
        item is BatchGroupViewModel ? GroupTemplate! : CardTemplate!;
}
