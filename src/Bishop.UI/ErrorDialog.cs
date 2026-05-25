using Bishop.App;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;

namespace Bishop.UI;

internal static class ErrorDialog
{
    private const string DefaultTitle = "I'm sorry, Dave. I'm afraid I can't do that.";

    public static async Task ShowAsync(XamlRoot xamlRoot, Exception ex, string? title = null)
    {
        var dialog = new ContentDialog
        {
            Title = title ?? DefaultTitle,
            Content = ExceptionDialogHelper.BuildErrorDialogText(ex),
            PrimaryButtonText = "Copy details",
            SecondaryButtonText = "Open log folder",
            CloseButtonText = "Dismiss",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };
        try
        {
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var pkg = new DataPackage();
                pkg.SetText($"{ex.GetType().FullName}\n{ex.Message}\n\n{ex}");
                Clipboard.SetContent(pkg);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Bishop.AI");
                Process.Start("explorer.exe", logDir);
            }
        }
        catch (COMException)
        {
            // ShowAsync throws COMException when a ContentDialog is already open; swallow.
        }
        catch (Exception showEx)
        {
            App.LogExceptionToFile(showEx);
        }
    }
}
