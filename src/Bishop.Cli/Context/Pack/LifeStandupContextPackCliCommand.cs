using Bishop.Life.Core;
using Bishop.Life.Core.Google;
using Bishop.Life.Core.Schema;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.Versioning;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Context.Pack;

internal sealed class LifeStandupContextPackCliCommand : Command
{
    internal const string ExpectedSchema = "bishop.life/v1";
    internal const int StarredCeiling = 3;
    internal const int CalendarHorizonDays = 14;
    internal static readonly TimeSpan CalendarTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public LifeStandupContextPackCliCommand(LifePlanFileService fileService, TimeProvider timeProvider)
        : this(fileService, timeProvider, calendarSource: ResolveDefaultCalendarSource())
    {
    }

    // Test seam — lets tests inject a fake calendar source without DPAPI/network.
    internal LifeStandupContextPackCliCommand(
        LifePlanFileService fileService,
        TimeProvider timeProvider,
        ICalendarSource? calendarSource)
        : base("life-standup", "Emit bishop.life stand-up context pack as JSON")
    {
        this.SetHandler(async (InvocationContext ctx) =>
        {
            try
            {
                var result = await LifeStandupContextPackBuilder.BuildAsync(
                    fileService, timeProvider, calendarSource, ctx.GetCancellationToken());
                Console.WriteLine(JsonSerializer.Serialize(result, s_jsonOpts));
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });
    }

    private static ICalendarSource? ResolveDefaultCalendarSource()
    {
        // Construction is best-effort: env vars unset or token missing → no calendar source, which
        // surfaces as calendar_unavailable: true. Auth failures don't crash the stand-up.
        if (!OperatingSystem.IsWindows()) return null;
        var settings = GoogleOAuthSettings.FromEnvironment();
        if (settings is null) return null;
        var store = new GoogleTokenStore();
        if (!store.Exists()) return null;
        return new GoogleCalendarService(settings, store);
    }
}

internal static class LifeStandupContextPackBuilder
{
    public static async Task<LifeStandupContextPack> BuildAsync(
        LifePlanFileService service,
        TimeProvider time,
        ICalendarSource? calendarSource,
        CancellationToken cancellationToken)
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
                Calendar: [],
                CalendarUnavailable: false,
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
                Calendar: [],
                CalendarUnavailable: false,
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

        var (calendar, calendarUnavailable) = await FetchCalendarAsync(calendarSource, now, cancellationToken);

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
            Calendar: calendar,
            CalendarUnavailable: calendarUnavailable,
            Plan: plan);
    }

    private static async Task<(IReadOnlyList<CalendarEventEntry> Events, bool Unavailable)> FetchCalendarAsync(
        ICalendarSource? source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // No source configured (unset env vars, missing token, or non-Windows) is not an error —
        // the standup just runs without calendar context.
        if (source is null) return ([], false);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(LifeStandupContextPackCliCommand.CalendarTimeout);

            var from = now;
            var to = now.AddDays(LifeStandupContextPackCliCommand.CalendarHorizonDays);
            var events = await source.FetchUpcomingAsync(from, to, cts.Token);

            return (events.Select(e => new CalendarEventEntry(
                Id: e.Id,
                Summary: e.Summary,
                Start: e.Start,
                End: e.End,
                AllDay: e.AllDay,
                Status: e.Status)).ToList(), false);
        }
        catch
        {
            // Any failure — timeout, HTTP error, expired token, scope revoked — surfaces as
            // calendar_unavailable. Stand-up must still run.
            return ([], true);
        }
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
    IReadOnlyList<CalendarEventEntry> Calendar,
    bool CalendarUnavailable,
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

internal sealed record CalendarEventEntry(
    string Id,
    string Summary,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool AllDay,
    string Status);
