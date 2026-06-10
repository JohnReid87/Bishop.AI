using Bishop.ViewModels.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Bishop.Tests.ViewModels.Shared;

public class SafeAsyncRunnerTests
{
    [Fact]
    public async Task RunAsync_LogsAtErrorLevel_WhenExceptionIsThrown()
    {
        var logger = new RecordingLogger<SafeAsyncRunner>();
        var runner = new SafeAsyncRunner(logger);
        var exception = new InvalidOperationException("boom");

        await runner.RunAsync(() => throw exception);

        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Exception == exception);
    }

    [Fact]
    public async Task RunAsync_LogsException_EvenWhenOnExceptionIsNull()
    {
        var logger = new RecordingLogger<SafeAsyncRunner>();
        var runner = new SafeAsyncRunner(logger, onException: null);
        var exception = new InvalidOperationException("silent failure");

        await runner.RunAsync(() => throw exception);

        logger.Entries.Should().ContainSingle(e => e.Exception == exception);
    }

    [Fact]
    public async Task RunAsync_InvokesOnException_AfterLogging()
    {
        var logger = new RecordingLogger<SafeAsyncRunner>();
        Exception? captured = null;
        var runner = new SafeAsyncRunner(logger, onException: ex => captured = ex);
        var exception = new InvalidOperationException("handled");

        await runner.RunAsync(() => throw exception);

        captured.Should().BeSameAs(exception);
        logger.Entries.Should().ContainSingle(e => e.Exception == exception);
    }

    [Fact]
    public async Task RunAsync_InvokesInjectedCallback_WithoutAnyStaticSetup()
    {
        // Proves the testability goal of card #852: a test exercising RunAsync can
        // route exceptions to a callback purely through constructor injection — no
        // process-wide static state to initialise or clean up between tests.
        var logger = new RecordingLogger<SafeAsyncRunner>();
        Exception? captured = null;
        var runner = new SafeAsyncRunner(logger, onException: ex => captured = ex);
        var exception = new InvalidOperationException("via DI");

        await runner.RunAsync(() => throw exception);

        captured.Should().BeSameAs(exception);
    }

    [Fact]
    public async Task RunAsync_DoesNotLog_WhenNoExceptionIsThrown()
    {
        var logger = new RecordingLogger<SafeAsyncRunner>();
        var runner = new SafeAsyncRunner(logger);

        await runner.RunAsync(() => Task.CompletedTask);

        logger.Entries.Should().BeEmpty();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<(LogLevel Level, Exception? Exception)> _entries = [];
        public IReadOnlyList<(LogLevel Level, Exception? Exception)> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => _entries.Add((logLevel, exception));
    }
}
