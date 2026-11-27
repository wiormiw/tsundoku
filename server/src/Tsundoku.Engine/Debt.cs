namespace Tsundoku.Engine;

/// <summary>Debt per activity: what a short day owes, how logs pay it, what today asks, and self-tuning (fold semantics 7).</summary>
internal static class Debt
{
    /// <summary>Float noise from 0.1 steps and 1.25 * 40 must not move a ceil, a floor or a comparison.</summary>
    internal const double Epsilon = 1e-9;

    /// <summary>
    /// The TS <c>Math.round(n * 1e6) / 1e6</c>. JS rounds a half up (toward +infinity), where Math.Round rounds it to even;
    /// <c>x - floor(x)</c> is exact for these magnitudes, so this matches bit for bit.
    /// </summary>
    internal static double Round(double n)
    {
        var x = n * 1e6;
        var whole = Math.Floor(x);
        return (x - whole >= 0.5 ? whole + 1 : whole) / 1e6;
    }

    private static double Outstanding(FoldState state, Guid activityId) =>
        state.Debt.Where(d => d.ActivityId == activityId).Sum(d => d.Amount);

    private static bool Owes(FoldState state, Guid areaId) => state.Debt.Any(d => d.AreaId == areaId);

    /// <summary>Today's asks: each due floor plus the debt it carries in, capped at <c>DailyCapRatio</c> of the floor.</summary>
    public static IReadOnlyList<GoalStateView.RequiredActivity> RequiredToday(FoldState state, IReadOnlyList<Classify.DueFloor> due)
    {
        var headroom = state.Rules.DailyCapRatio - 1;
        return [.. due.Select(d =>
        {
            var debtDue = Math.Min(Outstanding(state, d.ActivityId), Math.Floor(d.Floor * headroom + Epsilon));
            return new GoalStateView.RequiredActivity(d.ActivityId, d.Floor, debtDue, d.Floor + debtDue);
        })];
    }

    /// <summary>Outstanding debt as the view lists it, oldest first; overdue once past the due-by active day.</summary>
    public static IReadOnlyList<GoalStateView.DebtSummary> View(FoldState state) =>
        [.. state.Debt.Select(d => new GoalStateView.DebtSummary(d.ActivityId, d.Amount, state.ActiveDaysUsed > d.DueByK))];

    /// <summary>
    /// The debt step of a closed day. Logged amounts first fill the day's floor and the excess pays the activity's debt,
    /// oldest first (an activity not due that day pays too, and so does an offline day's log). Then each due activity's
    /// shortfall becomes new debt at the area's multiplier, escalated first when the area's previous non-offline day also
    /// created debt. A retrospective day does neither.
    /// </summary>
    public static void Step(FoldState state, FoldState.ClosedDay day)
    {
        var rules = state.Rules;
        if (day.Status == DayStatus.Retrospective)
        {
            for (var i = 0; i < state.Areas.Count; i++) state.Areas[i] = state.Areas[i] with { PaidDebtOnLastClosed = false };
            return;
        }
        var offline = day.Status == DayStatus.Offline;
        var due = offline ? [] : day.Due;
        double LoggedFor(Guid activityId) => day.Logged?.GetValueOrDefault(activityId) ?? 0;

        var paid = new HashSet<Guid>();
        foreach (var (activityId, amount) in day.Logged ?? new Dictionary<Guid, double>())
        {
            var excess = amount - (due.FirstOrDefault(d => d.ActivityId == activityId)?.Floor ?? 0);
            for (var i = 0; i < state.Debt.Count; i++)
            {
                if (excess <= Epsilon) break;
                var item = state.Debt[i];
                if (item.ActivityId != activityId) continue;
                var pay = Math.Min(excess, item.Amount);
                state.Debt[i] = item with { Amount = Round(item.Amount - pay) };
                excess -= pay;
                paid.Add(item.AreaId);
            }
        }
        state.Debt.RemoveAll(item => item.Amount <= Epsilon);

        for (var i = 0; i < state.Areas.Count; i++)
        {
            var area = state.Areas[i] with { PaidDebtOnLastClosed = paid.Contains(state.Areas[i].AreaId) };
            // Paying an area's debt off puts its multiplier back to base.
            if (area.PaidDebtOnLastClosed && !Owes(state, area.AreaId)) area = area with { Multiplier = area.Base };
            state.Areas[i] = area;
        }
        if (offline) return;

        for (var i = 0; i < state.Areas.Count; i++)
        {
            var area = state.Areas[i];
            var shortOnes = due.Where(d => d.AreaId == area.AreaId && d.Floor - LoggedFor(d.ActivityId) > 0).ToList();
            if (shortOnes.Count > 0 && area.NewDebtYesterday)
                area = area with { Multiplier = Round(Math.Min(rules.MultiplierMax, area.Multiplier + rules.EscalationStep)) };
            foreach (var d in shortOnes)
            {
                var shortfall = d.Floor - LoggedFor(d.ActivityId);
                state.Debt.Add(new FoldCarry.DebtItem(d.ActivityId, area.AreaId,
                    Math.Ceiling(shortfall * area.Multiplier - Epsilon), day.K, day.K + rules.DueWindowDays));
            }
            state.Areas[i] = area with { NewDebtYesterday = shortOnes.Count > 0 };
        }
    }

    /// <summary>
    /// Applies the ratings given for the last closed day: from the next day on, "easy" raises the area's base by
    /// <c>TuneStep</c>, "hard" lowers it, "right" leaves it, within [BaseMin, BaseMax]. A rating for a day the area paid no
    /// debt on is dropped, and so is one for an older day (the carry only knows whether the last closed day paid).
    /// Ratings for days not yet closed wait.
    /// </summary>
    public static void ApplyRatings(FoldState state)
    {
        if (state.LastClosedDate is not { } last) return;
        var rules = state.Rules;
        var due = state.PendingRatings.Where(r => r.Date == last).ToList();
        state.PendingRatings = [.. state.PendingRatings.Where(r => r.Date > last)];
        foreach (var rating in due)
        {
            var i = state.Areas.FindIndex(a => a.AreaId == rating.AreaId);
            if (i < 0 || !state.Areas[i].PaidDebtOnLastClosed) continue;
            var step = rating.Rating == DebtRating.Easy ? rules.TuneStep : rating.Rating == DebtRating.Hard ? -rules.TuneStep : 0;
            var area = state.Areas[i];
            area = area with { Base = Round(Math.Min(rules.BaseMax, Math.Max(rules.BaseMin, area.Base + step))) };
            // An area with no debt owes at its base, so the new base shows at once; one with debt keeps its escalated
            // multiplier until the debt is paid.
            if (!Owes(state, area.AreaId)) area = area with { Multiplier = area.Base };
            state.Areas[i] = area;
        }
    }
}
