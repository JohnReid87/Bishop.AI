using Bishop.App.Services.Terminal;
using NSubstitute;

namespace Bishop.Tests;

internal static class TestBootstrappers
{
    public static IWorkspaceBootstrapper NoOp => Substitute.For<IWorkspaceBootstrapper>();
}
