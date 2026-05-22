using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

internal sealed class KanbanSplitter : Grid
{
    public KanbanSplitter()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }
}
