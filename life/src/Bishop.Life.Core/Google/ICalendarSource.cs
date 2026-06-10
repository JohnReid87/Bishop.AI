namespace Bishop.Life.Core.Google;

/// <summary>
/// Indirection over <see cref="GoogleCalendarService.FetchUpcomingAsync"/> so the context-pack
/// builder can be unit-tested without touching the network or DPAPI.
/// </summary>
public interface ICalendarSource
{
    Task<IReadOnlyList<CalendarEvent>> FetchUpcomingAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}
