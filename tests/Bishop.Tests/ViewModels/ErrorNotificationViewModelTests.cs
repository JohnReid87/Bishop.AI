using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public class ErrorNotificationViewModelTests
{
    [Fact]
    public void Title_ReturnsBackgroundError()
    {
        var vm = NewVm();

        vm.Title.Should().Be("Background error");
    }

    [Fact]
    public void Timestamp_IsSetToApproximatelyNow_WhenCreated()
    {
        var vm = NewVm();

        vm.Timestamp.Should().BeCloseTo(DateTimeOffset.Now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Message_WhenExceptionHasNoMessage_IsHandledGracefully()
    {
        var vm = NewVm(ex: new Exception());

        vm.Message.Should().NotBeNull();
    }

    private static ErrorNotificationViewModel NewVm(
        Exception? ex = null,
        Action<Exception>? showDetails = null,
        Action<ErrorNotificationViewModel>? dismiss = null)
    {
        ex ??= new InvalidOperationException("test");
        showDetails ??= _ => { };
        dismiss ??= _ => { };
        return new ErrorNotificationViewModel(ex, showDetails, dismiss);
    }
}
