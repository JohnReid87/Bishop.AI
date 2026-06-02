using Bishop.ViewModels.Workspaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Bishop.UI.Views.Workspaces;

internal sealed class NotesSplitterDragHandler
{
    private readonly WorkspaceNotesViewModel _notes;
    private readonly UIElement _coordinateSource;
    private bool _isDragging;
    private double _dragStartPageY;
    private double _dragStartNoteHeight;

    public NotesSplitterDragHandler(WorkspaceNotesViewModel notes, UIElement coordinateSource)
    {
        _notes = notes;
        _coordinateSource = coordinateSource;
    }

    public void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = true;
        _dragStartPageY = e.GetCurrentPoint(_coordinateSource).Position.Y;
        _dragStartNoteHeight = _notes.PanelHeight;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    public void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        var delta = _dragStartPageY - e.GetCurrentPoint(_coordinateSource).Position.Y;
        _notes.PanelHeight = Math.Max(80, Math.Min(600, _dragStartNoteHeight + delta));
        e.Handled = true;
    }

    public void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    public void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
    }
}
