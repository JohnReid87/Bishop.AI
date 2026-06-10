using System.Runtime.CompilerServices;

namespace Bishop.Tests;

// Guards the whole test assembly against the classic "test blocks forever on Console.In" hang.
//
// Under `dotnet test`, the test host's stdin is a redirected-but-never-closed pipe, so
// Console.IsInputRedirected is true while Console.In.ReadToEndAsync() (a SyncTextReader over the
// synchronous console read) blocks waiting for an EOF that never arrives. Any CLI command a test
// invokes without piping input — e.g. CreateCardCliCommand reading the description from stdin — then
// deadlocks a thread, which stalls test-host shutdown until the blame-hang timeout fires. (Visual
// Studio's Test Explorer hides this because there Console.IsInputRedirected is false.)
//
// Pointing Console.In at the always-at-EOF TextReader.Null makes every such read return immediately.
// Tests that need real stdin content already call Console.SetIn(...) themselves and restore the
// previous reader, so this only changes the default for tests that never touch stdin.
internal static class TestConsoleInGuard
{
    [ModuleInitializer]
    internal static void NeutraliseConsoleStdin() => Console.SetIn(TextReader.Null);
}
