using System;
using System.Numerics;
using Microsoft.UI.Xaml;

namespace Bishop.UI.Animations;

/// <summary>
/// Implicit fade + scale entrance animations for cards and dialogs.
/// </summary>
/// <remarks>
/// <see cref="UIElement.OpacityTransition"/> and <see cref="UIElement.ScaleTransition"/>
/// are not DependencyProperties in Windows App SDK 1.6, so they cannot be set in XAML —
/// they must be assigned in code.
/// </remarks>
internal static class EntranceAnimation
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(150);
    private static readonly Vector3 StartScale = new(0.97f, 0.97f, 1f);

    public static void ApplyCardEntrance(FrameworkElement? element)
    {
        if (element is null) return;

        element.OpacityTransition ??= new ScalarTransition { Duration = Duration };
        element.ScaleTransition ??= new Vector3Transition { Duration = Duration };

        var target = element.Opacity;
        element.Opacity = 0;
        element.Scale = StartScale;

        element.DispatcherQueue.TryEnqueue(() =>
        {
            element.Opacity = target;
            element.Scale = Vector3.One;
        });
    }

    public static void ApplyDialogEntrance(FrameworkElement? element)
    {
        if (element is null) return;
        element.OpacityTransition ??= new ScalarTransition { Duration = Duration };
    }
}
