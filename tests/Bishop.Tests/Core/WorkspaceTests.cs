using System.Reflection;
using Bishop.Core;
using FluentAssertions;

namespace Bishop.Tests.Core;

public sealed class WorkspaceTests
{
    [Fact]
    public void With_path_override_preserves_every_other_public_property()
    {
        var original = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "ws",
            Path = @"C:\original",
            Position = 7,
            NextCardNumber = 42,
            GitHubRepo = "owner/repo",
            IsRemoved = true,
            RemovedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            Cards = new List<Card> { new() { Id = Guid.NewGuid(), Title = "c1" } },
        };

        var copy = original.With(path: @"C:\worktree");

        copy.Path.Should().Be(@"C:\worktree");

        var properties = typeof(Workspace)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != nameof(Workspace.Path));

        foreach (var prop in properties)
        {
            var originalValue = prop.GetValue(original);
            var copyValue = prop.GetValue(copy);
            copyValue.Should().BeEquivalentTo(originalValue, because: $"property {prop.Name} must survive With(path)");
        }
    }

    [Fact]
    public void With_no_arguments_preserves_path()
    {
        var original = new Workspace { Id = Guid.NewGuid(), Name = "ws", Path = @"C:\original" };

        var copy = original.With();

        copy.Path.Should().Be(@"C:\original");
    }
}
