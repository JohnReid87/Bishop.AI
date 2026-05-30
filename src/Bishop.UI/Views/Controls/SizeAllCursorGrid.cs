using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views.Controls;

internal sealed class SizeAllCursorGrid : Grid
{
    public SizeAllCursorGrid()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    }
}
