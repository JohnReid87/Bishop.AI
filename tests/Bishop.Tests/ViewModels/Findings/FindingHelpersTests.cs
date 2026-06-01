using Bishop.ViewModels.Findings;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.Findings;

public class FindingHelpersTests
{
    // --- FindingSeverityColor ---

    [Theory]
    [InlineData("critical", "#c97a8a")]
    [InlineData("high", "#c97a8a")]
    [InlineData("CRITICAL", "#c97a8a")]
    [InlineData("medium", "#c4a85f")]
    [InlineData("med", "#c4a85f")]
    [InlineData("low", "#5fa89c")]
    [InlineData("info", "#5fa89c")]
    [InlineData("other", "#9aa86a")]
    [InlineData(null, "#9aa86a")]
    public void SeverityColor_For_ReturnsExpectedColor(string? severity, string expected)
    {
        FindingSeverityColor.For(severity).Should().Be(expected);
    }

    // --- FindingStatusState ---

    [Theory]
    [InlineData("dismissed", null, "dismissed")]
    [InlineData("parked", null, "parked")]
    [InlineData("resolved", null, "resolved")]
    [InlineData("pending", null, "pending")]
    [InlineData("carded", null, "pending")]
    public void StatusState_For_StatusLabel_NoLinkedCard(string status, int? linkedCardId, string expected)
    {
        FindingStatusState.For(status, linkedCardId).StatusLabel.Should().Be(expected);
    }

    [Fact]
    public void StatusState_For_StatusLabel_WithLinkedCard_ReturnsCardRef()
    {
        FindingStatusState.For("carded", 42).StatusLabel.Should().Be("#42");
    }

    [Theory]
    [InlineData("pending", null, true)]
    [InlineData("dismissed", null, false)]
    [InlineData("resolved", null, false)]
    [InlineData("pending", 5, false)]
    public void StatusState_For_IsConvertToCardVisible(string status, int? linkedCardId, bool expected)
    {
        FindingStatusState.For(status, linkedCardId).IsConvertToCardVisible.Should().Be(expected);
    }

    [Theory]
    [InlineData("pending", null, true)]
    [InlineData("dismissed", null, false)]
    [InlineData("resolved", null, false)]
    [InlineData("pending", 3, false)]
    public void StatusState_For_IsDismissEnabled(string status, int? linkedCardId, bool expected)
    {
        FindingStatusState.For(status, linkedCardId).IsDismissEnabled.Should().Be(expected);
    }
}
