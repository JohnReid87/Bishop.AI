using Bishop.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Bishop.Tests.ViewModels;

public class SafeAsyncTests : IDisposable
{
    public void Dispose()
    {
        SafeAsync.Logger = null;
        SafeAsync.OnException = null;
    }

    [Fact]
    public async Task RunAsync_LogsAtErrorLevel_WhenExceptionIsThrown()
    {
        var logger = new RecordingLogger();
        SafeAsync.Logger = logger;
        var exception = new InvalidOperationException("boom");

        await SafeAsync.RunAsync(() => throw exception);

        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Exception == exception);
    }

    [Fact]
    public async Task RunAsync_LogsException_EvenWhenOnExceptionIsNull()
    {
        var logger = new RecordingLogger();
        SafeAsync.Logger = logger;
        SafeAsync.OnException = null;
        var exception = new InvalidOperationException("silent failure");

        await SafeAsync.RunAsync(() => throw exception);

        logger.Entries.Should().ContainSingle(e => e.Exception == exception);
    }

    [Fact]
    public async Task RunAsync_InvokesOnException_AfterLogging()
    {
        var logger = new RecordingLogger();
        SafeAsync.Logger = logger;
        Exception? captured = null;
        SafeAsync.OnException = ex => captured = ex;
        var exception = new InvalidOperationException("handled");

        await SafeAsync.RunAsync(() => throw exception);

        captured.Should().BeSameAs(exception);
        logger.Entries.Should().ContainSingle(e => e.Exception == exception);
    }

    [Fact]
    public async Task RunAsync_DoesNotThrow_WhenBothLoggerAndOnExceptionAreNull()
    {
        SafeAsync.Logger = null;
        SafeAsync.OnException = null;

        var act = () => SafeAsync.RunAsync(() => throw new InvalidOperationException("orphaned"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RunAsync_DoesNotLog_WhenNoExceptionIsThrown()
    {
        var logger = new RecordingLogger();
        SafeAsync.Logger = logger;

        await SafeAsync.RunAsync(() => Task.CompletedTask);

        logger.Entries.Should().BeEmpty();
    }

    private sealed class RecordingLogger : ILogger
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
