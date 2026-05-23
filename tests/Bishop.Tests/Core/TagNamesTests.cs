using Bishop.Core;
using FluentAssertions;

namespace Bishop.Tests.Core;

public class TagNamesTests
{
    [Fact]
    public void Feature_IsCorrectValue() =>
        TagNames.Feature.Should().Be("feature");

    [Fact]
    public void Bug_IsCorrectValue() =>
        TagNames.Bug.Should().Be("bug");

    [Fact]
    public void Chore_IsCorrectValue() =>
        TagNames.Chore.Should().Be("chore");

    [Fact]
    public void Docs_IsCorrectValue() =>
        TagNames.Docs.Should().Be("docs");

    [Fact]
    public void Arch_IsCorrectValue() =>
        TagNames.Arch.Should().Be("arch");

    [Fact]
    public void Test_IsCorrectValue() =>
        TagNames.Test.Should().Be("test");

    [Fact]
    public void Spike_IsCorrectValue() =>
        TagNames.Spike.Should().Be("spike");

    [Fact]
    public void All_ContainsExactlySevenTags() =>
        TagNames.All.Should().HaveCount(7);

    [Fact]
    public void All_ContainsAllTagsInOrder() =>
        TagNames.All.Should().ContainInOrder(
            "feature", "bug", "chore", "docs", "arch", "test", "spike");

    [Fact]
    public void All_TagsReferenceConstantValues() =>
        TagNames.All.Should().BeEquivalentTo(
            [TagNames.Feature, TagNames.Bug, TagNames.Chore, TagNames.Docs, TagNames.Arch, TagNames.Test, TagNames.Spike],
            opts => opts.WithStrictOrdering());
}
