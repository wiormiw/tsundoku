namespace Tsundoku.Engine;

/// <summary>The status of a closed day in a running goal (fold semantics 3).</summary>
internal static class Classify
{
    /// <summary>A due activity's floor on a day, already scaled by its area's replan scale.</summary>
    internal sealed record DueFloor(Guid AreaId, Guid ActivityId, int Floor);

    /// <param name="marked">The net offline mark for the day, honouring the deadline.</param>
    /// <param name="hasRoom">Whether the month's allowance has room for one more offline or skipped day.</param>
    /// <param name="logged">Amounts logged on the day by activity; null when nothing was logged (an amount of 0 is a log).</param>
    /// <param name="due">What the day asks.</param>
    public static DayStatus Day(bool marked, bool hasRoom, IReadOnlyDictionary<Guid, double>? logged, IReadOnlyList<DueFloor> due)
    {
        if (marked && hasRoom) return DayStatus.Offline;
        if (logged is null) return hasRoom ? DayStatus.Skipped : DayStatus.Missed;
        return due.All(d => logged.GetValueOrDefault(d.ActivityId) >= d.Floor) ? DayStatus.Met : DayStatus.Short;
    }
}
