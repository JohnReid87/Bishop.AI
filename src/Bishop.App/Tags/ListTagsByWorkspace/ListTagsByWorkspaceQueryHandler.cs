using Bishop.Core;
using MediatR;

namespace Bishop.App.Tags.ListTagsByWorkspace;

public sealed class ListTagsByWorkspaceQueryHandler : IRequestHandler<ListTagsByWorkspaceQuery, IReadOnlyList<TagInfo>>
{
    private static readonly IReadOnlyList<TagInfo> Tags = BrandTagPalette.DefaultColours
        .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
        .Select(kv => new TagInfo(kv.Key, kv.Value))
        .ToList();

    public Task<IReadOnlyList<TagInfo>> Handle(ListTagsByWorkspaceQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Tags);
}
