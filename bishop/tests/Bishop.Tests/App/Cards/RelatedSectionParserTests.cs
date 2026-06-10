using Bishop.App.Cards;
using FluentAssertions;

namespace Bishop.Tests.App.Cards;

public sealed class RelatedSectionParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmpty_ReturnsEmpty(string? description)
    {
        RelatedSectionParser.ParseCardNumbers(description).Should().BeEmpty();
    }

    [Fact]
    public void NoRelatedSection_ReturnsEmpty()
    {
        var result = RelatedSectionParser.ParseCardNumbers("### Why\nSome motivation\n### Acceptance\n- criterion");
        result.Should().BeEmpty();
    }

    [Fact]
    public void HashPrefixedRef_ReturnsNumber()
    {
        var result = RelatedSectionParser.ParseCardNumbers("### Related\n- #42");
        result.Should().ContainSingle().Which.Should().Be(42);
    }

    [Fact]
    public void BareNumber_ReturnsNumber()
    {
        var result = RelatedSectionParser.ParseCardNumbers("### Related\n- 42");
        result.Should().ContainSingle().Which.Should().Be(42);
    }

    [Fact]
    public void MultipleRefs_ReturnsAllNumbers()
    {
        var result = RelatedSectionParser.ParseCardNumbers("### Related\n- #42\n- 7\n- #123");
        result.Should().BeEquivalentTo(new[] { 42, 7, 123 });
    }

    [Fact]
    public void UrlNoise_IsIgnored()
    {
        var result = RelatedSectionParser.ParseCardNumbers("### Related\nhttps://github.com/foo/bar/issues/42");
        result.Should().BeEmpty();
    }

    [Fact]
    public void StopsAtNextH3_DoesNotReadBeyondRelatedSection()
    {
        var result = RelatedSectionParser.ParseCardNumbers("### Related\n- #42\n### Out of scope\n- #99");
        result.Should().ContainSingle().Which.Should().Be(42);
    }

    [Fact]
    public void RelatedSectionAtEnd_IsRead()
    {
        var result = RelatedSectionParser.ParseCardNumbers("### Why\nReason\n### Related\n- #10\n- #20");
        result.Should().BeEquivalentTo(new[] { 10, 20 });
    }
}
