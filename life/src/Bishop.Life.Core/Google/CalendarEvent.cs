namespace Bishop.Life.Core.Google;

/// <summary>
/// A Google Calendar event reduced to the fields stand-up needs. Descriptions and attendee
/// lists are stripped at fetch time — the stand-up prompts on title + when, nothing else.
/// </summary>
public sealed record CalendarEvent(
    string Id,
    string Summary,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool AllDay,
    string Status);
