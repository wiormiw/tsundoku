using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

/// <summary>
/// What the fold needs to continue after <see cref="LastClosedDate"/> (S), kept apart from the view so a client never
/// depends on it. <see cref="LastSeq"/> is the highest seq among the facts whose local day is at or before S. To resume,
/// fold every fact from the start of S+1 in <see cref="Timezone"/> onto this carry.
/// </summary>
public sealed record FoldCarry(
    // The rules the state was computed under (Rules.Key); a snapshot under other rules is stale.
    string RulesKey,
    DateOnly StartDate,
    string Timezone,
    int RangeActiveDays,
    // The goal's own cap on offline plus skipped days per month; null for none.
    int? OfflinePerMonthCap,
    // Each from the date it applies; the last one at or before a day governs it.
    IReadOnlyList<FoldCarry.Plan> Plans,
    // The net offline mark per date, for days not yet closed.
    IReadOnlyList<FoldCarry.PendingMark> PendingMarks,
    IReadOnlyList<Fact.DebtRated.Payload> PendingRatings,
    // In creation order; payment takes the oldest item first.
    IReadOnlyList<FoldCarry.DebtItem> Debt,
    IReadOnlyList<FoldCarry.AreaCarry> Areas,
    int ActiveDaysUsed,
    int OfflineUsed,
    int SkippedUsed,
    int ProlongedDays,
    int RetrospectiveDays,
    IReadOnlyList<MonthUsage> MonthUsage,
    // The last day the fold classified; null before the first.
    DateOnly? LastClosedDate,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] long LastSeq)
{
    public sealed record Plan(DateOnly EffectiveFrom, Curriculum Curriculum);

    public sealed record PendingMark(DateOnly Date, bool Marked);

    /// <summary>Owed in the activity's unit; overdue once active days used pass <c>DueByK</c> (<c>CreatedK + DueWindowDays</c>).</summary>
    public sealed record DebtItem(Guid ActivityId, Guid AreaId, double Amount, int CreatedK, int DueByK);

    /// <summary>
    /// An area's moving parts between days. <c>ReplanScale</c> is the product of the bankruptcy replan scales;
    /// <c>NewDebtYesterday</c> drives escalation; <c>PaidDebtOnLastClosed</c> lets a rating for the last closed day apply.
    /// </summary>
    public sealed record AreaCarry(Guid AreaId, double Multiplier, double Base, double ReplanScale, bool NewDebtYesterday, bool PaidDebtOnLastClosed);
}
