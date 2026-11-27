using System.Globalization;

namespace Tsundoku.Engine;

/// <summary>A goal's local calendar, in its IANA timezone. Matches Temporal, the TS reference, at DST transitions.</summary>
public static class Calendar
{
    /// <summary>The goal's local date for an instant.</summary>
    public static DateOnly LocalDate(DateTimeOffset instant, string timezone) => LocalDate(instant, TimeZoneInfo.FindSystemTimeZoneById(timezone));

    /// <summary>As above, for a zone the caller resolved once (a fold resolves its goal's zone once, not per fact).</summary>
    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>Whether an instant is before <paramref name="date"/> 00:00 in the timezone: the deadline for an offline mark.</summary>
    public static bool IsBeforeLocalMidnight(DateTimeOffset instant, DateOnly date, string timezone) =>
        IsBeforeLocalMidnight(instant, date, TimeZoneInfo.FindSystemTimeZoneById(timezone));

    /// <inheritdoc cref="IsBeforeLocalMidnight(DateTimeOffset, DateOnly, string)"/>
    public static bool IsBeforeLocalMidnight(DateTimeOffset instant, DateOnly date, TimeZoneInfo zone) => instant < LocalMidnight(date, zone);

    /// <summary><c>YYYY-MM</c>, the key of a calendar month.</summary>
    public static string MonthKey(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>Offline plus skipped days a month allows: a third of its days, bounded by the goal's own cap.</summary>
    public static int AllowanceCap(DateOnly date, int? goalCap)
    {
        var share = DateTime.DaysInMonth(date.Year, date.Month) / 3;
        return goalCap is { } cap ? Math.Min(cap, share) : share;
    }

    /// <summary>Whether <paramref name="date"/>'s month still has an offline or skipped day to give.</summary>
    public static bool HasAllowanceRoom(int? offlinePerMonthCap, IEnumerable<MonthUsage> monthUsage, DateOnly date)
    {
        var month = MonthKey(date);
        var used = monthUsage.FirstOrDefault(m => m.Month == month)?.Used ?? 0;
        return used < AllowanceCap(date, offlinePerMonthCap);
    }

    /// <summary>As above, from the IANA id.</summary>
    public static DateTimeOffset LocalMidnight(DateOnly date, string timezone) => LocalMidnight(date, TimeZoneInfo.FindSystemTimeZoneById(timezone));

    /// <summary>
    /// The instant local midnight starts <paramref name="date"/>, resolved like Temporal's "compatible": a midnight inside a
    /// spring-forward gap, or one that happens twice, takes the offset in force before the transition.
    /// </summary>
    public static DateTimeOffset LocalMidnight(DateOnly date, TimeZoneInfo zone)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        // shortcut: assumes no other transition within a day before this one; true of every IANA zone in use.
        var offset = zone.IsInvalidTime(midnight) || zone.IsAmbiguousTime(midnight)
            ? zone.GetUtcOffset(new DateTimeOffset(midnight.AddDays(-1), TimeSpan.Zero))
            : zone.GetUtcOffset(midnight);
        return new DateTimeOffset(midnight, offset);
    }
}
