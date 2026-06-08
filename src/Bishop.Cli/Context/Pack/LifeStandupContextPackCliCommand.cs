using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Context.Pack;

internal sealed class LifeStandupContextPackCliCommand : Command
{
    internal const string ExpectedSchema = "bishop.life/v1";
    internal const int StarredCeiling = 3;

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public LifeStandupContextPackCliCommand(LifePlanFileService fileService, TimeProvider timeProvider)
        : base("life-standup", "Emit bishop.life stand-up context pack as JSON")
    {
        this.SetHandler((InvocationContext ctx) =>
        {
            try
            {
                var result = LifeStandupContextPackBuilder.Build(fileService, timeProvider);
                Console.WriteLine(JsonSerializer.Serialize(result, s_jsonOpts));
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });
    }
}

internal static class LifeStandupContextPackBuilder
{
    public static LifeStandupContextPack Build(LifePlanFileService service, TimeProvider time)
    {
        var path = service.FilePath;

        if (!service.Exists())
        {
            return new LifeStandupContextPack(
                FilePath: path,
                Exists: false,
                SchemaOk: false,
                Schema: null,
                ExpectedSchema: LifeStandupContextPackCliCommand.ExpectedSchema,
                LastStandupAt: null,
                LastStandupPhrase: null,
                OpenActionCount: 0,
                StarredCeiling: LifeStandupContextPackCliCommand.StarredCeiling,
                Starred: [],
                UntendedAreas: [],
                Inbox: [],
                Plan: null);
        }

        var plan = service.Load();
        var schemaOk = string.Equals(plan.Schema, LifeStandupContextPackCliCommand.ExpectedSchema, StringComparison.Ordinal);

        if (!schemaOk)
        {
            return new LifeStandupContextPack(
                FilePath: path,
                Exists: true,
                SchemaOk: false,
                Schema: plan.Schema,
                ExpectedSchema: LifeStandupContextPackCliCommand.ExpectedSchema,
                LastStandupAt: null,
                LastStandupPhrase: null,
                OpenActionCount: 0,
                StarredCeiling: LifeStandupContextPackCliCommand.StarredCeiling,
                Starred: [],
                UntendedAreas: [],
                Inbox: [],
                Plan: null);
        }

        var now = time.GetUtcNow();
        var allActions = plan.Areas
            .SelectMany(a => a.Goals.SelectMany(g => g.Actions.Select(act => (Area: a, Goal: g, Action: act))))
            .ToList();

        var openActionCount = allActions.Count(t => !t.Action.Done);

        var starred = allActions
            .Where(t => t.Action.Starred && !t.Action.Done)
            .Select(t => new StarredActionEntry(
                ActionId: t.Action.Id,
                Area: t.Area.Name,
                Goal: t.Goal.Name,
                Title: t.Action.Title,
                Horizon: t.Action.Horizon))
            .ToList();

        var untendedAreas = plan.Areas
            .Where(a => a.Goals.SelectMany(g => g.Actions).All(act => act.Done))
            .Select(a => a.Name)
            .ToList();

        var inbox = plan.Inbox
            .Select(i => new InboxEntry(i.Id, i.Text, i.CapturedAt))
            .ToList();

        return new LifeStandupContextPack(
            FilePath: path,
            Exists: true,
            SchemaOk: true,
            Schema: plan.Schema,
            ExpectedSchema: LifeStandupContextPackCliCommand.ExpectedSchema,
            LastStandupAt: plan.Meta.LastStandupAt,
            LastStandupPhrase: LastStandupPhrase(plan.Meta.LastStandupAt, now),
            OpenActionCount: openActionCount,
            StarredCeiling: LifeStandupContextPackCliCommand.StarredCeiling,
            Starred: starred,
            UntendedAreas: untendedAreas,
            Inbox: inbox,
            Plan: plan);
    }

    internal static string LastStandupPhrase(DateTimeOffset? last, DateTimeOffset now)
    {
        if (last is null) return "first stand-up";
        var delta = now - last.Value;
        if (delta < TimeSpan.Zero) return "today";
        var days = (int)Math.Floor(delta.TotalDays);
        return days switch
        {
            0 => "today",
            1 => "yesterday",
            _ => $"{days} days ago"
        };
    }
}

internal sealed record LifeStandupContextPack(
    string FilePath,
    bool Exists,
    bool SchemaOk,
    string? Schema,
    string ExpectedSchema,
    DateTimeOffset? LastStandupAt,
    string? LastStandupPhrase,
    int OpenActionCount,
    int StarredCeiling,
    IReadOnlyList<StarredActionEntry> Starred,
    IReadOnlyList<string> UntendedAreas,
    IReadOnlyList<InboxEntry> Inbox,
    LifePlan? Plan);

internal sealed record StarredActionEntry(
    string ActionId,
    string Area,
    string Goal,
    string Title,
    Horizon Horizon);

internal sealed record InboxEntry(
    string Id,
    string Text,
    DateTimeOffset CapturedAt);
