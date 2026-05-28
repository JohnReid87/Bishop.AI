namespace Bishop.App;

public static class ExceptionDialogHelper
{
    public static string BuildErrorDialogText(Exception ex) =>
        $"{ex.GetType().Name}: {ex.Message}";
}
