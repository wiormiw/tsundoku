namespace Tsundoku.Engine;

/// <summary>What a curriculum asks of a day.</summary>
public static class Schedule
{
    /// <summary>Absorbs float noise before rounding up, as the TS engine does: 10 * 0.8 is 8.000000000000002.</summary>
    private const double Epsilon = 1e-9;

    /// <summary>The milestone covering active day <paramref name="k"/> (0-based), or null past the last one.</summary>
    public static Milestone? MilestoneAt(Area area, int k)
    {
        var start = 0;
        foreach (var milestone in area.Milestones)
        {
            if (k < start + milestone.Days) return milestone;
            start += milestone.Days;
        }
        return null;
    }

    /// <summary>Activities due on <paramref name="date"/>, the day with active-day number <paramref name="k"/>: in the milestone and on cadence.</summary>
    public static IReadOnlyList<DueActivity> DueActivities(Curriculum curriculum, int k, DateOnly date)
    {
        var weekday = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        return [.. curriculum.Areas
            .Select(area => (area, milestone: MilestoneAt(area, k)))
            .Where(pair => pair.milestone is not null)
            .SelectMany(pair => pair.milestone!.Activities
                .Where(activity => activity.Cadence switch
                {
                    Cadence.Daily => true,
                    Cadence.Weekly weekly => weekly.Weekdays.Contains(weekday),
                })
                .Select(activity => new DueActivity(pair.area.Id, activity, pair.milestone!.Id)))];
    }

    /// <summary>A floor times a replan scale, rounded up and at least 1.</summary>
    public static int ScaledFloor(int floor, double scale) => Math.Max(1, (int)Math.Ceiling(floor * scale - Epsilon));
}
