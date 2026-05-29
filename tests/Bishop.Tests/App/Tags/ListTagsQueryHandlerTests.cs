using Bishop.App.Tags.ListTags;
using Bishop.Core;
using FluentAssertions;

namespace Bishop.Tests.App.Tags;

public sealed class ListTagsQueryHandlerTests
{
    private static ListTagsQueryHandler CreateSut() => new();

    [Fact]
    public async Task Handle_ReturnsAllDefaultTags()
    {
        var sut = CreateSut();

        var result = await sut.Handle(new ListTagsQuery(), CancellationToken.None);

        result.Should().HaveCount(BrandTagPalette.DefaultColours.Count);
    }

    [Fact]
    public async Task Handle_TagsAreOrderedAlphabetically()
    {
        var sut = CreateSut();

        var result = await sut.Handle(new ListTagsQuery(), CancellationToken.None);

        result.Select(t => t.Name).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_TagNamesAndColoursMatchPalette()
    {
        var sut = CreateSut();

        var result = await sut.Handle(new ListTagsQuery(), CancellationToken.None);

        var expected = BrandTagPalette.DefaultColours
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new TagInfo(kv.Key, kv.Value));

        result.Should().BeEquivalentTo(expected, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task Handle_CancellationToken_DoesNotThrow()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.Handle(new ListTagsQuery(), cts.Token);

        await act.Should().NotThrowAsync();
    }
}
