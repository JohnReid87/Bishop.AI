using Bishop.Life.Core;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class LifeClaudeArgsTests
{
    [Fact]
    public void Standup_IncludesSessionIdAndBypassPermissions()
    {
        var args = LifeClaudeArgs.Standup("abc-123");

        args.Should().Contain("/bish-life-standup");
        args.Should().Contain("--session-id abc-123");
        args.Should().Contain("--permission-mode bypassPermissions");
    }

    [Fact]
    public void Init_DoesNotIncludeBypassPermissions()
    {
        var args = LifeClaudeArgs.Init();

        args.Should().Be("/bish-life-init");
        args.Should().NotContain("bypassPermissions");
    }

    [Fact]
    public void Add_DoesNotIncludeBypassPermissions()
    {
        var args = LifeClaudeArgs.Add();

        args.Should().Be("/bish-life-add");
        args.Should().NotContain("bypassPermissions");
    }
}
