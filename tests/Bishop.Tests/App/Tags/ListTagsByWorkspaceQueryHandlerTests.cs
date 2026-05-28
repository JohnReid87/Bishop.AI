using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.Core;
using FluentAssertions;

namespace Bishop.Tests.App.Tags;

public sealed class ListTagsByWorkspaceQueryHandlerTests
{
    private static ListTagsByWorkspaceQueryHandler CreateSut() => new();

    [Fact]
    public async Task Handle_ReturnsAllDefaultTags()
    {
        var sut = CreateSut();

        var result = await sut.Handle(new ListTagsByWorkspaceQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().HaveCount(BrandTagPalette.DefaultColours.Count);
    }

    [Fact]
    public async Task Handle_TagsAreOrderedAlphabetically()
    {
        var sut = CreateSut();

        var result = await sut.Handle(new ListTagsByWorkspaceQuery(Guid.NewGuid()), CancellationToken.None);

        result.Select(t => t.Name).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_TagNamesAndColoursMatchPalette()
    {
        var sut = CreateSut();

        var result = await sut.Handle(new ListTagsByWorkspaceQuery(Guid.NewGuid()), CancellationToken.None);

        var expected = BrandTagPalette.DefaultColours
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new TagInfo(kv.Key, kv.Value));

        result.Should().BeEquivalentTo(expected, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task Handle_WorkspaceIdDoesNotAffectResult()
    {
        var sut = CreateSut();

        var resultA = await sut.Handle(new ListTagsByWorkspaceQuery(Guid.NewGuid()), CancellationToken.None);
        var resultB = await sut.Handle(new ListTagsByWorkspaceQuery(Guid.NewGuid()), CancellationToken.None);

        resultA.Should().BeEquivalentTo(resultB, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task Handle_CancellationToken_DoesNotThrow()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.Handle(new ListTagsByWorkspaceQuery(Guid.NewGuid()), cts.Token);

        await act.Should().NotThrowAsync();
    }
}
