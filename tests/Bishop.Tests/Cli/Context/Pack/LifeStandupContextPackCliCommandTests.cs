using System.Text.Json;
using Bishop.Cli.Context.Pack;
using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using System.CommandLine;

namespace Bishop.Tests.Cli.Context.Pack;

[Collection("ConsoleTests")]
public sealed class LifeStandupContextPackCliCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _planPath;
    private readonly FakeTimeProvider _time;

    public LifeStandupContextPackCliCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bishop-life-standup-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _planPath = Path.Combine(_tempDir, "bishop.life.json");
        _time = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-08T10:00:00Z"));
    }

    public void Dispose()
    {
        // Best-effort temp-dir cleanup; tests run in parallel and a sibling
        // process may still hold a handle on Windows. Swallowing IO failures
        // here keeps the test result honest — a stale temp dir is harmless.
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"warn: temp-dir cleanup deferred: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"warn: temp-dir cleanup deferred: {ex.Message}");
        }
    }

    [Fact]
    public async Task InvokeAsync_FileMissing_EmitsExistsFalseAndExitsZero()
    {
        var cmd = new LifeStandupContextPackCliCommand(new LifePlanFileService(_planPath), _time);

        var json = await RunAndCaptureStdoutAsync(cmd);

        json.RootElement.GetProperty("exists").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("filePath").GetString().Should().Be(_planPath);
        json.RootElement.GetProperty("schemaOk").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_SchemaMismatch_EmitsSchemaOkFalseAndSurfacesActualSchema()
    {
        File.WriteAllText(_planPath, """{ "schema": "bishop.life/v999", "meta": {}, "areas": [], "inbox": [], "standups": [] }""");
        var cmd = new LifeStandupContextPackCliCommand(new LifePlanFileService(_planPath), _time);

        var json = await RunAndCaptureStdoutAsync(cmd);

        json.RootElement.GetProperty("exists").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("schemaOk").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("schema").GetString().Should().Be("bishop.life/v999");
        json.RootElement.GetProperty("expectedSchema").GetString().Should().Be("bishop.life/v1");
    }

    [Fact]
    public async Task InvokeAsync_PopulatedPlan_DerivesContextCorrectly()
    {
        var plan = new LifePlan
        {
            Schema = "bishop.life/v1",
            Meta = new Meta
            {
                CreatedAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
                LastStandupAt = DateTimeOffset.Parse("2026-06-06T10:00:00Z"),
            },
            Areas =
            {
                new Area
                {
                    Id = "area-finances", Name = "Finances", Color = "#aaa",
                    Goals = { new Goal { Id = "goal-1", Name = "Save",
                        Actions = {
                            new LifeAction { Id = "act-1", Title = "Move £500", Starred = true, Done = false, Horizon = Horizon.ThisWeek },
                            new LifeAction { Id = "act-2", Title = "Tally receipts", Done = true },
                        } } }
                },
                new Area
                {
                    Id = "area-career", Name = "Career", Color = "#bbb",
                    Goals = { new Goal { Id = "goal-2", Name = "Ship",
                        Actions = { new LifeAction { Id = "act-3", Title = "Ship PR", Starred = false, Done = false, Horizon = Horizon.Today } } } }
                },
                new Area // untended — only done actions
                {
                    Id = "area-home", Name = "Home", Color = "#ccc",
                    Goals = { new Goal { Id = "goal-3", Name = "Tidy",
                        Actions = { new LifeAction { Id = "act-4", Title = "Sweep", Done = true } } } }
                },
                new Area // untended — no actions at all
                {
                    Id = "area-health", Name = "Health", Color = "#ddd",
                    Goals = { },
                },
            },
            Inbox = { new InboxItem { Id = "ibx-1", Text = "Look into ISA limits", CapturedAt = _time.GetUtcNow() } },
        };
        File.WriteAllText(_planPath, LifePlanJson.Serialize(plan));

        var cmd = new LifeStandupContextPackCliCommand(new LifePlanFileService(_planPath), _time);

        var json = await RunAndCaptureStdoutAsync(cmd);
        var root = json.RootElement;

        root.GetProperty("exists").GetBoolean().Should().BeTrue();
        root.GetProperty("schemaOk").GetBoolean().Should().BeTrue();
        root.GetProperty("openActionCount").GetInt32().Should().Be(2);
        root.GetProperty("starredCeiling").GetInt32().Should().Be(3);
        root.GetProperty("lastStandupPhrase").GetString().Should().Be("2 days ago");

        var starred = root.GetProperty("starred");
        starred.GetArrayLength().Should().Be(1);
        starred[0].GetProperty("actionId").GetString().Should().Be("act-1");
        starred[0].GetProperty("area").GetString().Should().Be("Finances");
        starred[0].GetProperty("goal").GetString().Should().Be("Save");
        starred[0].GetProperty("title").GetString().Should().Be("Move £500");
        starred[0].GetProperty("horizon").GetString().Should().Be("thisWeek");

        var untended = root.GetProperty("untendedAreas").EnumerateArray().Select(e => e.GetString()).ToList();
        untended.Should().BeEquivalentTo(["Home", "Health"]);

        var inbox = root.GetProperty("inbox");
        inbox.GetArrayLength().Should().Be(1);
        inbox[0].GetProperty("id").GetString().Should().Be("ibx-1");
        inbox[0].GetProperty("text").GetString().Should().Be("Look into ISA limits");

        root.GetProperty("plan").GetProperty("schema").GetString().Should().Be("bishop.life/v1");
    }

    [Theory]
    [InlineData(null, "first stand-up")]
    [InlineData("2026-06-08T09:00:00Z", "today")]
    [InlineData("2026-06-07T10:00:00Z", "yesterday")]
    [InlineData("2026-06-05T10:00:00Z", "3 days ago")]
    public void LastStandupPhrase_FormatsHumanRelativeTime(string? lastIso, string expected)
    {
        var now = DateTimeOffset.Parse("2026-06-08T10:00:00Z");
        DateTimeOffset? last = lastIso is null ? null : DateTimeOffset.Parse(lastIso);

        LifeStandupContextPackBuilder.LastStandupPhrase(last, now).Should().Be(expected);
    }

    [Fact]
    public async Task InvokeAsync_MalformedJson_ExitsOneWithStderr()
    {
        File.WriteAllText(_planPath, "{ not json");
        var cmd = new LifeStandupContextPackCliCommand(new LifePlanFileService(_planPath), _time);

        var stderr = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(stderr);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync([]);
        }
        finally
        {
            Console.SetError(originalErr);
        }

        exitCode.Should().Be(1);
        stderr.ToString().Should().StartWith("error:");
    }

    private static async Task<JsonDocument> RunAndCaptureStdoutAsync(LifeStandupContextPackCliCommand cmd)
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            var exit = await cmd.InvokeAsync([]);
            exit.Should().Be(0);
        }
        finally
        {
            Console.SetOut(original);
        }
        return JsonDocument.Parse(output.ToString());
    }
}
