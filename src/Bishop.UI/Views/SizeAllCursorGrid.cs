using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

internal sealed class SizeAllCursorGrid : Grid
{
    public SizeAllCursorGrid()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    }
}
