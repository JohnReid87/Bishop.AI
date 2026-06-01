using Bishop.App.Context.ContextPack;
using FluentAssertions;

namespace Bishop.Tests.App.Context;

public sealed class StaticContextSectionsTests
{
    [Fact]
    public void Slice_UnknownSection_ErrorMessageListsSectionNamesQuotedAndCommaDelimited()
    {
        var act = () => StaticContextSections.Slice(["Nonexistent"]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown context section*Nonexistent*")
            .WithMessage("*\"Shell selection\", \"Card Push Procedure\"*");
    }

    [Fact]
    public void Slice_KnownSection_ReturnsSection()
    {
        var result = StaticContextSections.Slice(["Shell selection"]);

        result.Should().ContainKey("Shell selection");
        result["Shell selection"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Slice_KnownSectionCaseInsensitive_ReturnsSection()
    {
        var result = StaticContextSections.Slice(["shell selection"]);

        result.Should().ContainKey("shell selection");
    }

    [Fact]
    public void Slice_CalledMultipleTimes_ParsesEmbeddedResourceOnce()
    {
        _ = StaticContextSections.Slice(["Shell selection"]);
        var before = StaticContextSections.ParseInvocationCount;

        _ = StaticContextSections.Slice(["Shell selection"]);
        _ = StaticContextSections.Slice(["Card Push Procedure"]);

        StaticContextSections.ParseInvocationCount.Should().Be(before);
    }
}
