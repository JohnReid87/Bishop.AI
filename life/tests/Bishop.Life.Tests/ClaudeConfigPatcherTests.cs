using System.Text.Json.Nodes;
using Bishop.Life.Core;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class ClaudeConfigPatcherTests
{
    [Fact]
    public void EnsureBypassPermissionsAccepted_AbsentFile_IsNoOp()
    {
        using var dir = new TempDir();
        var path = dir.FilePath(".claude.json");

        var act = () => ClaudeConfigPatcher.EnsureBypassPermissionsAccepted(path);

        act.Should().NotThrow();
        File.Exists(path).Should().BeFalse("absent file should be left untouched — claude isn't set up");
    }

    [Fact]
    public void EnsureBypassPermissionsAccepted_MissingKey_AddsKeyAndPreservesOtherContent()
    {
        using var dir = new TempDir();
        var path = dir.FilePath(".claude.json");
        File.WriteAllText(path, """
            {
              "theme": "dark",
              "projects": { "x": { "y": 1 } }
            }
            """);

        ClaudeConfigPatcher.EnsureBypassPermissionsAccepted(path);

        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root["bypassPermissionsModeAccepted"]!.GetValue<bool>().Should().BeTrue();
        root["theme"]!.GetValue<string>().Should().Be("dark");
        root["projects"]!["x"]!["y"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public void EnsureBypassPermissionsAccepted_KeyAlreadyTrue_LeavesFileUntouched()
    {
        using var dir = new TempDir();
        var path = dir.FilePath(".claude.json");
        var original = """
            {
              "bypassPermissionsModeAccepted": true,
              "theme": "dark"
            }
            """;
        File.WriteAllText(path, original);

        ClaudeConfigPatcher.EnsureBypassPermissionsAccepted(path);

        File.ReadAllText(path).Should().Be(original, "file should not be rewritten when key is already true");
    }

    [Fact]
    public void EnsureBypassPermissionsAccepted_KeyFalse_FlipsToTrue()
    {
        using var dir = new TempDir();
        var path = dir.FilePath(".claude.json");
        File.WriteAllText(path, """
            {
              "bypassPermissionsModeAccepted": false,
              "theme": "dark"
            }
            """);

        ClaudeConfigPatcher.EnsureBypassPermissionsAccepted(path);

        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root["bypassPermissionsModeAccepted"]!.GetValue<bool>().Should().BeTrue();
        root["theme"]!.GetValue<string>().Should().Be("dark");
    }

    [Fact]
    public void EnsureBypassPermissionsAccepted_NonObjectRoot_IsNoOp()
    {
        using var dir = new TempDir();
        var path = dir.FilePath(".claude.json");
        File.WriteAllText(path, "[1, 2, 3]");

        var act = () => ClaudeConfigPatcher.EnsureBypassPermissionsAccepted(path);

        act.Should().NotThrow();
        File.ReadAllText(path).Should().Be("[1, 2, 3]");
    }
}
