using Bishop.Core;
using MediatR;

namespace Bishop.App.Tags.ListTags;

public sealed record ListTagsQuery : IRequest<IReadOnlyList<TagInfo>>;
