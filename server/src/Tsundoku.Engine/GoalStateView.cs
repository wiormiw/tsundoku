using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

/// <summary>The fold's output and a snapshot's state, in the TS <c>GoalStateView</c> wire shape.</summary>
public sealed record GoalStateView(
    GoalPhase Phase,
    DateOnly Today,
    DateOnly EndDate,
    int ActiveDaysUsed,
    int ActiveDaysTotal,
    // One entry per closed day.
    IReadOnlyList<GoalStateView.GoalDay> Days,
    IReadOnlyList<GoalStateView.RequiredActivity> Required,
    // Today is net-marked offline with allowance room, so nothing is required of it.
    bool OfflineToday,
    // Dates, today or later, whose net offline mark is set, sorted.
    IReadOnlyList<DateOnly> MarkedOffline,
    IReadOnlyList<GoalStateView.DebtSummary> Debt,
    IReadOnlyList<GoalStateView.AreaSummary> Areas,
    IReadOnlyList<GoalStateView.MonthAllowance> Allowance,
    IReadOnlyList<GoalStateView.Bankruptcy> Bankruptcies,
    // Repeats the carry's.
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] long LastSeq,
    FoldCarry Carry)
{
    /// <summary>One closed day; <c>K</c> is the active-day number, null for days that are not active.</summary>
    public sealed record GoalDay(DateOnly Date, DayStatus Status, int? K);

    /// <summary>What today asks of an activity: its floor, the debt it carries in, and the sum.</summary>
    public sealed record RequiredActivity(Guid ActivityId, int Floor, double DebtDue, double Total);

    public sealed record DebtSummary(Guid ActivityId, double Amount, bool Overdue);

    public sealed record AreaSummary(Guid AreaId, double Multiplier, double Base, double ReplanScale);

    /// <summary>A month's offline plus skipped days used against its cap.</summary>
    public sealed record MonthAllowance(string Month, int Used, int Cap);

    public sealed record Bankruptcy(Guid AreaId, DateOnly Date);
}
