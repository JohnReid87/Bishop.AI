using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views.Controls;

internal sealed class NotesSplitter : Grid
{
    public NotesSplitter()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    }
}
