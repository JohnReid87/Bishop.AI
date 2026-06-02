using Bishop.App.Findings.RecordFindings;
using FluentAssertions;

namespace Bishop.Tests.App.Findings;

public sealed class FindingOutcomeParserTests
{
    [Fact]
    public void Parse_Dismissed_ReturnsDismissedWithNullCard()
    {
        var (status, card) = FindingOutcomeParser.Parse("dismissed");

        status.Should().Be("dismissed");
        card.Should().BeNull();
    }

    [Theory]
    [InlineData("carded:#1", 1)]
    [InlineData("carded:#42", 42)]
    [InlineData("carded:#12345", 12345)]
    public void Parse_CardedWithPositiveNumber_ReturnsCardedAndNumber(string outcome, int expected)
    {
        var (status, card) = FindingOutcomeParser.Parse(outcome);

        status.Should().Be("carded");
        card.Should().Be(expected);
    }

    [Theory]
    [InlineData("carded:#0")]
    [InlineData("carded:#-3")]
    [InlineData("carded:#")]
    [InlineData("carded:#abc")]
    public void Parse_CardedWithInvalidNumber_FallsBackToPending(string outcome)
    {
        var (status, card) = FindingOutcomeParser.Parse(outcome);

        status.Should().Be("pending");
        card.Should().BeNull();
    }

    [Theory]
    [InlineData("parked")]
    [InlineData("")]
    [InlineData("something-else")]
    public void Parse_OtherValues_ReturnPending(string outcome)
    {
        var (status, card) = FindingOutcomeParser.Parse(outcome);

        status.Should().Be("pending");
        card.Should().BeNull();
    }
}
