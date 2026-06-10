using System.Threading;
using System.Threading.Tasks;

namespace Bishop.Life.Core.Web;

/// <summary>
/// Abstraction over the host→WebView2 message channel. Production implementation
/// in <c>Bishop.Life.App</c> wraps <c>CoreWebView2.PostWebMessageAsJson</c>;
/// tests substitute a fake to assert envelope shape without spinning up
/// WebView2. Implementations are responsible for JSON serialization and any
/// UI-thread marshalling.
/// </summary>
public interface IBrowserChannel
{
    Task PostAsync(object envelope, CancellationToken ct = default);
}
