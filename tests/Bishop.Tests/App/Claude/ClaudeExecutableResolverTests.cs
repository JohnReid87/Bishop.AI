using Bishop.App.Claude;
using FluentAssertions;

namespace Bishop.Tests.App.Claude;

public sealed class ClaudeExecutableResolverTests
{
    private static string Join(params string[] dirs) => string.Join(Path.PathSeparator, dirs);

    private static bool PathEquals(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Resolve_ReturnsExePath_WhenExeIsPresent_OnWindows()
    {
        var exe = Path.Combine("C:\\bin", "claude.exe");
        var env = MakeEnv(path: Join("C:\\bin"), pathExt: ".COM;.EXE;.BAT;.CMD");
        var sut = new ClaudeExecutableResolver(env, p => PathEquals(p, exe), isWindows: true);

        var result = sut.Resolve();

        result.Should().BeEquivalentTo(exe);
    }

    [Fact]
    public void Resolve_ReturnsCmdPath_WhenOnlyCmdShimIsPresent_OnWindows()
    {
        var cmd = Path.Combine("C:\\npm", "claude.cmd");
        var env = MakeEnv(path: Join("C:\\npm"), pathExt: ".COM;.EXE;.BAT;.CMD");
        var sut = new ClaudeExecutableResolver(env, p => PathEquals(p, cmd), isWindows: true);

        var result = sut.Resolve();

        result.Should().BeEquivalentTo(cmd);
    }

    [Fact]
    public void Resolve_HonoursPathExtOrder_OnWindows()
    {
        var bat = Path.Combine("C:\\bin", "claude.bat");
        var exe = Path.Combine("C:\\bin", "claude.exe");
        var env = MakeEnv(path: Join("C:\\bin"), pathExt: ".BAT;.EXE");
        var probed = new List<string>();
        bool FileExists(string p)
        {
            probed.Add(p);
            return PathEquals(p, bat) || PathEquals(p, exe);
        }

        var sut = new ClaudeExecutableResolver(env, FileExists, isWindows: true);

        var result = sut.Resolve();

        PathEquals(result, bat).Should().BeTrue();
        PathEquals(probed[0], bat).Should().BeTrue();
    }

    [Fact]
    public void Resolve_UsesDefaultPathExt_WhenPathExtUnset_OnWindows()
    {
        var cmd = Path.Combine("C:\\bin", "claude.CMD");
        var env = MakeEnv(path: Join("C:\\bin"), pathExt: null);
        var sut = new ClaudeExecutableResolver(env, p => PathEquals(p, cmd), isWindows: true);

        var result = sut.Resolve();

        result.Should().BeEquivalentTo(cmd);
    }

    [Fact]
    public void Resolve_IgnoresEmptyAndWhitespacePathEntries()
    {
        var exe = Path.Combine("C:\\real", "claude.exe");
        var pathWithGaps = string.Join(Path.PathSeparator, new[] { "", "  ", "C:\\real", "" });
        var env = MakeEnv(path: pathWithGaps, pathExt: ".EXE");
        var probed = new List<string>();
        bool FileExists(string p)
        {
            probed.Add(p);
            return PathEquals(p, exe);
        }

        var sut = new ClaudeExecutableResolver(env, FileExists, isWindows: true);

        var result = sut.Resolve();

        PathEquals(result, exe).Should().BeTrue();
        probed.Should().OnlyContain(p => p.StartsWith("C:\\real"));
    }

    [Fact]
    public void Resolve_Throws_WithPopulatedCandidatesAndDirectories_OnMiss_OnWindows()
    {
        var env = MakeEnv(path: Join("C:\\a", "C:\\b"), pathExt: ".EXE;.CMD");
        var sut = new ClaudeExecutableResolver(env, _ => false, isWindows: true);

        var act = () => sut.Resolve();

        var ex = act.Should().Throw<ClaudeNotFoundException>().Which;
        ex.Candidates.Should().Equal("claude.EXE", "claude.CMD");
        ex.Directories.Should().Equal("C:\\a", "C:\\b");
    }

    [Fact]
    public void Resolve_OnUnix_ProbesClaudeWithoutExtension_IgnoringPathExt()
    {
        var unixPath = Path.Combine("/usr/local/bin", "claude");
        var env = MakeEnv(path: Join("/usr/local/bin"), pathExt: ".EXE;.CMD");
        var probed = new List<string>();
        bool FileExists(string p)
        {
            probed.Add(p);
            return p == unixPath;
        }

        var sut = new ClaudeExecutableResolver(env, FileExists, isWindows: false);

        var result = sut.Resolve();

        result.Should().Be(unixPath);
        probed.Should().ContainSingle().Which.Should().Be(unixPath);
    }

    [Fact]
    public void Resolve_Throws_WithPopulatedCandidatesAndDirectories_OnMiss_OnUnix()
    {
        var env = MakeEnv(path: Join("/usr/local/bin", "/usr/bin"), pathExt: null);
        var sut = new ClaudeExecutableResolver(env, _ => false, isWindows: false);

        var act = () => sut.Resolve();

        var ex = act.Should().Throw<ClaudeNotFoundException>().Which;
        ex.Candidates.Should().Equal("claude");
        ex.Directories.Should().Equal("/usr/local/bin", "/usr/bin");
    }

    [Fact]
    public void DefaultCtor_Resolve_UsesRealEnvironmentDependencies()
    {
        var sut = new ClaudeExecutableResolver();

        string? result = null;
        ClaudeNotFoundException? miss = null;
        try { result = sut.Resolve(); }
        catch (ClaudeNotFoundException ex) { miss = ex; }

        if (result is not null)
        {
            File.Exists(result).Should().BeTrue("the resolved path must exist on disk");
            Path.IsPathRooted(result).Should().BeTrue("the resolved path must be absolute");
            Path.GetFileNameWithoutExtension(result).Should().BeEquivalentTo("claude",
                "the resolved executable must be named claude");
        }
        else
        {
            var expectedDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator)
                .Select(d => d.Trim())
                .Where(d => d.Length > 0)
                .ToList();

            miss!.Directories.Should().BeEquivalentTo(expectedDirs,
                "directories must reflect the real PATH environment variable");
        }
    }

    [Fact]
    public void Resolve_CachesResolvedPath_AcrossCalls()
    {
        var exe = Path.Combine("C:\\bin", "claude.exe");
        var env = MakeEnv(path: Join("C:\\bin"), pathExt: ".EXE");
        var probeCount = 0;
        bool FileExists(string p)
        {
            probeCount++;
            return PathEquals(p, exe);
        }

        var sut = new ClaudeExecutableResolver(env, FileExists, isWindows: true);

        var first = sut.Resolve();
        var second = sut.Resolve();

        PathEquals(first, exe).Should().BeTrue();
        second.Should().Be(first);
        probeCount.Should().Be(1);
    }

    [Fact]
    public void Resolve_ReturnsCachedPath_WithoutReprobing()
    {
        var exe = Path.Combine("C:\\bin", "claude.exe");
        var env = MakeEnv(path: Join("C:\\bin"), pathExt: ".EXE");
        var fileExists = true;
        var sut = new ClaudeExecutableResolver(env, p => fileExists && PathEquals(p, exe), isWindows: true);

        var first = sut.Resolve();
        fileExists = false; // subsequent probes would throw if re-probing occurred
        var second = sut.Resolve();

        second.Should().Be(first);
        PathEquals(second, exe).Should().BeTrue();
    }

    [Fact]
    public void Resolve_Throws_WhenPathIsNull()
    {
        var env = MakeEnv(path: null, pathExt: ".EXE");
        var sut = new ClaudeExecutableResolver(env, _ => false, isWindows: true);

        var act = () => sut.Resolve();

        var ex = act.Should().Throw<ClaudeNotFoundException>().Which;
        ex.Directories.Should().BeEmpty();
        ex.Candidates.Should().Equal("claude.EXE");
    }

    [Fact]
    public void Resolve_Throws_WhenPathIsEmpty()
    {
        var env = MakeEnv(path: "", pathExt: ".EXE");
        var sut = new ClaudeExecutableResolver(env, _ => false, isWindows: true);

        var act = () => sut.Resolve();

        var ex = act.Should().Throw<ClaudeNotFoundException>().Which;
        ex.Directories.Should().BeEmpty();
        ex.Candidates.Should().Equal("claude.EXE");
    }

    [Fact]
    public void Resolve_Throws_WhenPathContainsOnlyEmptyAndWhitespaceEntries()
    {
        var allWhitespace = string.Join(Path.PathSeparator, new[] { "", "  ", "" });
        var env = MakeEnv(path: allWhitespace, pathExt: ".EXE");
        var sut = new ClaudeExecutableResolver(env, _ => false, isWindows: true);

        var act = () => sut.Resolve();

        var ex = act.Should().Throw<ClaudeNotFoundException>().Which;
        ex.Directories.Should().BeEmpty();
    }

    private static Func<string, string?> MakeEnv(string? path, string? pathExt)
    {
        return key => key switch
        {
            "PATH" => path,
            "PATHEXT" => pathExt,
            _ => null,
        };
    }
}
