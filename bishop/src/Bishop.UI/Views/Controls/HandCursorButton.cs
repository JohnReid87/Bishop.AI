using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views.Controls;

internal sealed class HandCursorButton : Button
{
    public HandCursorButton()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}
