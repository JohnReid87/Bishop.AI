using System.Runtime.Versioning;
using global::Google.Apis.Auth.OAuth2;
using global::Google.Apis.Auth.OAuth2.Flows;
using global::Google.Apis.Auth.OAuth2.Responses;
using global::Google.Apis.Calendar.v3;
using global::Google.Apis.Calendar.v3.Data;
using global::Google.Apis.Services;

namespace Bishop.Life.Core.Google;

/// <summary>
/// Drives the installed-app OAuth flow (loopback redirect) on first run, then uses the stored
/// refresh token to fetch upcoming events from the user's primary calendar. Scope is read-only.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GoogleCalendarService : ICalendarSource
{
    public const string ApplicationName = "Bishop.Life";
    public static readonly IReadOnlyList<string> Scopes = [CalendarService.Scope.CalendarReadonly];

    private readonly GoogleOAuthSettings _settings;
    private readonly GoogleTokenStore _tokenStore;

    public GoogleCalendarService(GoogleOAuthSettings settings, GoogleTokenStore tokenStore)
    {
        _settings = settings;
        _tokenStore = tokenStore;
    }

    /// <summary>
    /// Runs the installed-app OAuth flow — opens a loopback HTTP listener, launches the user's
    /// browser to Google's consent page, and persists the resulting refresh token via DPAPI.
    /// </summary>
    public async Task AuthorizeAsync(CancellationToken cancellationToken)
    {
        var clientSecrets = new ClientSecrets
        {
            ClientId = _settings.ClientId,
            ClientSecret = _settings.ClientSecret,
        };

        // No FileDataStore — we manage the refresh token ourselves via GoogleTokenStore (DPAPI).
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            clientSecrets,
            Scopes,
            user: "bishop-life",
            cancellationToken,
            dataStore: null);

        var refreshToken = credential.Token.RefreshToken
            ?? throw new InvalidOperationException(
                "Google did not return a refresh token. Revoke the existing grant at " +
                "https://myaccount.google.com/permissions and try again so consent is re-prompted.");

        _tokenStore.SaveRefreshToken(refreshToken);
    }

    /// <summary>
    /// Fetches events from the primary calendar between <paramref name="from"/> and
    /// <paramref name="to"/>. Filters out declined events; keeps accepted, tentative, and
    /// needsAction. Strips descriptions and attendee lists.
    /// </summary>
    public async Task<IReadOnlyList<CalendarEvent>> FetchUpcomingAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var refreshToken = _tokenStore.LoadRefreshToken()
            ?? throw new InvalidOperationException(
                "No Google refresh token found. Run `bishop life auth google` first.");

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = _settings.ClientId, ClientSecret = _settings.ClientSecret },
            Scopes = Scopes,
        });

        var tokenResponse = new TokenResponse { RefreshToken = refreshToken };
        var credential = new UserCredential(flow, "bishop-life", tokenResponse);

        using var service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });

        var request = service.Events.List("primary");
        request.TimeMinDateTimeOffset = from;
        request.TimeMaxDateTimeOffset = to;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        request.MaxResults = 250;

        var response = await request.ExecuteAsync(cancellationToken);
        var events = response.Items ?? [];

        var results = new List<CalendarEvent>(events.Count);
        foreach (var ev in events)
        {
            var status = AttendeeStatusForSelf(ev);
            if (string.Equals(status, "declined", StringComparison.OrdinalIgnoreCase))
                continue;

            var (start, end, allDay) = NormaliseTimes(ev);
            if (start is null || end is null)
                continue;

            results.Add(new CalendarEvent(
                Id: ev.Id ?? string.Empty,
                Summary: ev.Summary ?? "(no title)",
                Start: start.Value,
                End: end.Value,
                AllDay: allDay,
                Status: status));
        }

        return results;
    }

    private static string AttendeeStatusForSelf(Event ev)
    {
        // When the calendar owner is also an attendee, Google marks the entry with self=true.
        // For events the user created with no other attendees, there is no attendees list — treat
        // those as accepted.
        if (ev.Attendees is null) return "accepted";
        foreach (var attendee in ev.Attendees)
        {
            if (attendee.Self == true)
                return attendee.ResponseStatus ?? "needsAction";
        }
        return "accepted";
    }

    private static (DateTimeOffset? Start, DateTimeOffset? End, bool AllDay) NormaliseTimes(Event ev)
    {
        if (ev.Start is null || ev.End is null) return (null, null, false);

        if (ev.Start.DateTimeDateTimeOffset is { } startTimed && ev.End.DateTimeDateTimeOffset is { } endTimed)
            return (startTimed, endTimed, false);

        // All-day events use Date (YYYY-MM-DD) with no time component. Treat as midnight local.
        if (!string.IsNullOrEmpty(ev.Start.Date) && !string.IsNullOrEmpty(ev.End.Date)
            && DateTime.TryParse(ev.Start.Date, out var startDay)
            && DateTime.TryParse(ev.End.Date, out var endDay))
        {
            var local = TimeZoneInfo.Local;
            var startOffset = new DateTimeOffset(startDay.Date, local.GetUtcOffset(startDay.Date));
            var endOffset = new DateTimeOffset(endDay.Date, local.GetUtcOffset(endDay.Date));
            return (startOffset, endOffset, true);
        }

        return (null, null, false);
    }
}
