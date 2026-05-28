using Bishop.App;
using FluentAssertions;

namespace Bishop.Tests.App;

public sealed class ExceptionDialogHelperTests
{
    [Fact]
    public void BuildErrorDialogText_PlainException_ReturnsTypeNameAndMessage()
    {
        var ex = new InvalidOperationException("something went wrong");

        var text = ExceptionDialogHelper.BuildErrorDialogText(ex);

        text.Should().Be("InvalidOperationException: something went wrong");
    }

    [Fact]
    public void BuildErrorDialogText_ExceptionWithInnerException_ReturnsOuterTypeNameAndMessage()
    {
        var inner = new ArgumentNullException("param");
        var ex = new InvalidOperationException("outer message", inner);

        var text = ExceptionDialogHelper.BuildErrorDialogText(ex);

        text.Should().Be("InvalidOperationException: outer message");
    }
}
