namespace Tsundoku.Engine;

/// <summary>
/// The fold's working state: the carry (what a snapshot keeps) as mutable lists of immutable records, plus what only
/// the days since the start need. Days are folded in order and mutate it; <see cref="Clone"/> stands for the TS
/// <c>structuredClone</c>, and since every element is immutable, copying the lists is a deep copy.
/// </summary>
internal sealed class FoldState
{
    /// <summary>A retro fact waiting for the day it takes effect (the day after its own).</summary>
    internal closed record ControlEvent(DateOnly Date)
    {
        internal sealed record Prolonged(DateOnly Date, int ExtraActiveDays) : ControlEvent(Date);

        internal sealed record Ended(DateOnly Date) : ControlEvent(Date);
    }

    /// <summary>A day just closed, with what the debt step needs: its active-day number (a skipped day's too), what was due, every amount logged.</summary>
    internal sealed record ClosedDay(DateOnly Date, DayStatus Status, int K, IReadOnlyList<Classify.DueFloor> Due, IReadOnlyDictionary<Guid, double>? Logged);

    private FoldState(Rules rules, GoalPhase phase, FoldCarry carry, IEnumerable<GoalStateView.GoalDay> days, IEnumerable<GoalStateView.Bankruptcy> bankruptcies)
    {
        Rules = rules;
        Phase = phase;
        RulesKey = carry.RulesKey;
        StartDate = carry.StartDate;
        Timezone = carry.Timezone;
        RangeActiveDays = carry.RangeActiveDays;
        OfflinePerMonthCap = carry.OfflinePerMonthCap;
        Plans = [.. carry.Plans];
        PendingMarks = [.. carry.PendingMarks];
        PendingRatings = [.. carry.PendingRatings];
        Debt = [.. carry.Debt];
        Areas = [.. carry.Areas];
        ActiveDaysUsed = carry.ActiveDaysUsed;
        OfflineUsed = carry.OfflineUsed;
        SkippedUsed = carry.SkippedUsed;
        ProlongedDays = carry.ProlongedDays;
        RetrospectiveDays = carry.RetrospectiveDays;
        MonthUsage = [.. carry.MonthUsage];
        LastClosedDate = carry.LastClosedDate;
        LastSeq = carry.LastSeq;
        Days = [.. days];
        Bankruptcies = [.. bankruptcies];
    }

    public Rules Rules { get; }
    public GoalPhase Phase { get; set; }
    public List<GoalStateView.GoalDay> Days { get; }
    public List<GoalStateView.Bankruptcy> Bankruptcies { get; }

    /// <summary>Amounts logged per local date and activity, for days not yet closed.</summary>
    public Dictionary<DateOnly, Dictionary<Guid, double>> DayLogs { get; } = [];

    public List<ControlEvent> Controls { get; } = [];

    // The carry, field for field.
    public string RulesKey { get; }
    public DateOnly StartDate { get; }
    public string Timezone { get; }
    public int RangeActiveDays { get; }
    public int? OfflinePerMonthCap { get; }
    public List<FoldCarry.Plan> Plans { get; }
    public List<FoldCarry.PendingMark> PendingMarks { get; set; }
    public List<Fact.DebtRated.Payload> PendingRatings { get; set; }
    public List<FoldCarry.DebtItem> Debt { get; set; }
    public List<FoldCarry.AreaCarry> Areas { get; }
    public int ActiveDaysUsed { get; set; }
    public int OfflineUsed { get; set; }
    public int SkippedUsed { get; set; }
    public int ProlongedDays { get; set; }
    public int RetrospectiveDays { get; set; }
    public List<MonthUsage> MonthUsage { get; }
    public DateOnly? LastClosedDate { get; set; }
    public long LastSeq { get; set; }

    public static FoldState New(Fact.GoalCreated created, Rules rules)
    {
        var data = created.Data;
        var areas = data.Areas.Select(area => new FoldCarry.AreaCarry(
            area.Id, rules.BaseMultiplier, rules.BaseMultiplier, ReplanScale: 1, NewDebtYesterday: false, PaidDebtOnLastClosed: false));
        var carry = new FoldCarry(Engine.Rules.Key(rules), data.StartDate, data.Timezone, data.RangeActiveDays, data.Caps.OfflinePerMonth,
            [], [], [], [], [.. areas], 0, 0, 0, 0, 0, [], null, 0);
        return new FoldState(rules, GoalPhase.Running, carry, [], []);
    }

    /// <summary>The state a snapshot stands for; the snapshot itself is never touched.</summary>
    public static FoldState Resume(GoalStateView snapshot, Rules rules) =>
        snapshot.Carry.RulesKey == Engine.Rules.Key(rules)
            ? new FoldState(rules, snapshot.Phase, snapshot.Carry, snapshot.Days, snapshot.Bankruptcies)
            : throw new ArgumentException("foldGoal: the snapshot was computed under other rules; fold from scratch", nameof(snapshot));

    public FoldState Clone()
    {
        var copy = new FoldState(Rules, Phase, ToCarry(), Days, Bankruptcies);
        foreach (var (date, logs) in DayLogs) copy.DayLogs[date] = new Dictionary<Guid, double>(logs);
        copy.Controls.AddRange(Controls);
        return copy;
    }

    public FoldCarry ToCarry() => new(RulesKey, StartDate, Timezone, RangeActiveDays, OfflinePerMonthCap,
        [.. Plans], [.. PendingMarks], [.. PendingRatings], [.. Debt], [.. Areas],
        ActiveDaysUsed, OfflineUsed, SkippedUsed, ProlongedDays, RetrospectiveDays, [.. MonthUsage], LastClosedDate, LastSeq);
}
