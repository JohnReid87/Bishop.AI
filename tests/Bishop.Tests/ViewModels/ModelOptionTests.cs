using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public class ModelOptionTests
{
    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var option = new ModelOption("claude-sonnet-4-6", "Claude Sonnet");

        option.Id.Should().Be("claude-sonnet-4-6");
        option.Label.Should().Be("Claude Sonnet");
    }

    [Fact]
    public void ToString_ReturnsLabel()
    {
        var option = new ModelOption("claude-sonnet-4-6", "Claude Sonnet");

        option.ToString().Should().Be("Claude Sonnet");
    }

    [Fact]
    public void EqualityAndHashCode_BasedOnIdAndLabel()
    {
        var a = new ModelOption("id1", "Label One");
        var b = new ModelOption("id1", "Label One");
        var c = new ModelOption("id2", "Label Two");

        a.Should().Be(b);
        a.Should().NotBe(c);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
