using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.Errors;

public class ErrorBusTests
{
    [Fact]
    public async Task Report_FromBackgroundThread_AddsNotificationToCollection()
    {
        var dispatcher = new SynchronousDispatcher();
        var bus = new ErrorBus(dispatcher, TimeProvider.System);
        var ex = new InvalidOperationException("boom");

        await Task.Run(() => bus.Report(ex));

        bus.Notifications.Should().HaveCount(1);
        bus.Notifications[0].Exception.Should().BeSameAs(ex);
        bus.Notifications[0].Message.Should().Be("boom");
        bus.Notifications[0].IsOpen.Should().BeTrue();
    }

    [Fact]
    public void Report_MultipleExceptions_PreservesOrder()
    {
        var dispatcher = new SynchronousDispatcher();
        var bus = new ErrorBus(dispatcher, TimeProvider.System);
        var ex1 = new InvalidOperationException("first");
        var ex2 = new ArgumentException("second");
        var ex3 = new TimeoutException("third");

        bus.Report(ex1);
        bus.Report(ex2);
        bus.Report(ex3);

        bus.Notifications.Should().HaveCount(3);
        bus.Notifications[0].Message.Should().Be("first");
        bus.Notifications[1].Message.Should().Be("second");
        bus.Notifications[2].Message.Should().Be("third");
    }

    [Fact]
    public void Dismiss_RemovesNotificationFromCollection()
    {
        var dispatcher = new SynchronousDispatcher();
        var bus = new ErrorBus(dispatcher, TimeProvider.System);
        bus.Report(new InvalidOperationException("one"));
        bus.Report(new InvalidOperationException("two"));

        bus.Notifications[0].IsOpen = false;

        bus.Notifications.Should().HaveCount(1);
        bus.Notifications[0].Message.Should().Be("two");
    }

    [Fact]
    public void ShowDetails_CallsShowDetailsHandler()
    {
        var dispatcher = new SynchronousDispatcher();
        var bus = new ErrorBus(dispatcher, TimeProvider.System);
        Exception? received = null;
        bus.ShowDetailsHandler = ex => received = ex;

        var thrown = new InvalidOperationException("details");
        bus.Report(thrown);
        bus.Notifications[0].ShowDetailsCommand.Execute(null);

        received.Should().BeSameAs(thrown);
    }

    [Fact]
    public void ShowDetails_WithNullHandler_DoesNotThrow()
    {
        var dispatcher = new SynchronousDispatcher();
        var bus = new ErrorBus(dispatcher, TimeProvider.System);
        bus.ShowDetailsHandler = null;
        bus.Report(new InvalidOperationException("x"));

        var act = () => bus.Notifications[0].ShowDetailsCommand.Execute(null);

        act.Should().NotThrow();
    }

    private sealed class SynchronousDispatcher : IUiDispatcher
    {
        public void TryEnqueue(Action work) => work();
        public void TryEnqueue(Func<Task> work) => work().GetAwaiter().GetResult();
    }
}
