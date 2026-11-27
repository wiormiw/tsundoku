using System.Globalization;

namespace Tsundoku.Engine;

/// <summary>The fold: a goal's facts into its state as of <c>today</c>. A port of the TS <c>foldGoal</c>, decision for decision.</summary>
public static class Fold
{
    /// <summary>
    /// Folds a goal's facts into its state as of <c>Today</c>. With a <c>Snapshot</c> it resumes after the snapshot's last
    /// closed day S: the facts are then those from the start of S+1 on, and the result equals a full fold of every fact
    /// under the same rules. A snapshot under other rules, or a fact on a day at or before S (a late one the snapshot
    /// cannot take in), is a caller bug and throws: the caller folds from scratch or from an earlier snapshot.
    /// </summary>
    public static GoalStateView FoldGoal(FoldInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var snapshot = input.Snapshot;
        var state = snapshot is null
            ? FoldState.New(
                input.Facts.OfType<Fact.GoalCreated>().FirstOrDefault()
                    ?? throw new ArgumentException("foldGoal: no goal.created fact", nameof(input)),
                input.Rules)
            : FoldState.Resume(snapshot, input.Rules);
        var today = input.Today;
        var start = state.StartDate;
        var entries = Ordered(input.Facts, state.Zone);
        var resumedAfter = snapshot is null ? null : state.LastClosedDate;
        if (resumedAfter is { } s && entries.FirstOrDefault(e => e.Date <= s) is { Fact: { } late } lateEntry)
        {
            throw new ArgumentException(
                $"foldGoal: fact {late.Id} is on {Iso(lateEntry.Date)}, at or before the snapshot's last closed day {Iso(s)}; resume from an earlier snapshot",
                nameof(input));
        }
        var first = resumedAfter?.AddDays(1) ?? start;

        var next = 0;
        // The highest seq ingested so far: every fact ingested by the end of day D is on a day <= D.
        var ingestedMax = 0L;
        void IngestThrough(DateOnly limit)
        {
            for (; next < entries.Count && entries[next].Date <= limit; next++)
            {
                Ingest(state, entries[next]);
                ingestedMax = Math.Max(ingestedMax, entries[next].Fact.Seq);
            }
        }
        IngestThrough(start < today ? start.AddDays(-1) : today.AddDays(-1));

        for (var day = first; day < today; day = day.AddDays(1))
        {
            if (state.Phase == GoalPhase.Ended) break;
            IngestThrough(day);
            // A rating given for yesterday takes effect on today's debt, so it lands before today closes.
            Debt.ApplyRatings(state);
            var closed = CloseDay(state, day);
            Debt.Step(state, closed);
            ApplyBankruptcies(state, closed);
            ApplyControls(state, day);
            if (input.OnDayClosed is { } onDayClosed)
            {
                state.LastSeq = Math.Max(ingestedMax, state.LastSeq);
                // On a copy: the closing fold applies ratings once it has no more days, and applying them early here
                // must not change how the days after this one close.
                var ending = state.Clone();
                Debt.ApplyRatings(ending);
                var opening = day.AddDays(1);
                onDayClosed(BuildView(ending, opening, start, OpenDay(ending, entries.Skip(next), opening)));
            }
        }

        Debt.ApplyRatings(state);
        // A resume only holds the facts after S, so the older ones' highest seq comes from the snapshot.
        state.LastSeq = Math.Max(HighestSeq(entries, state.LastClosedDate), state.LastSeq);
        return BuildView(state, today, start, OpenDay(state, entries.Skip(next), today));
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// The state the open day reports from: <paramref name="state"/> plus the facts dated today, which are not classified
    /// yet (a plan approved today, a mark made today for tomorrow). They go into a copy, so the carry, and every snapshot
    /// made from it, never holds a fact the next fold takes in again (Ruling W).
    /// </summary>
    private static FoldState OpenDay(FoldState state, IEnumerable<(Fact Fact, DateOnly Date)> rest, DateOnly today)
    {
        var todays = rest.Where(e => e.Date <= today).ToList();
        if (todays.Count == 0) return state;
        var open = state.Clone();
        foreach (var entry in todays) Ingest(open, entry);
        return open;
    }

    /// <summary>Facts in fold order (occurredAt, then seq; stable, as JS sort is), each with its local date.</summary>
    private static List<(Fact Fact, DateOnly Date)> Ordered(IReadOnlyList<Fact> facts, TimeZoneInfo zone)
    {
        // A synced log arrives in order already, and then no sort (and no key arrays) is needed.
        var sorted = true;
        for (var i = 1; i < facts.Count && sorted; i++)
            sorted = facts[i - 1].OccurredAt < facts[i].OccurredAt
                || (facts[i - 1].OccurredAt == facts[i].OccurredAt && facts[i - 1].Seq <= facts[i].Seq);
        var ordered = sorted ? facts : [.. facts.OrderBy(f => f.OccurredAt).ThenBy(f => f.Seq)];
        var entries = new List<(Fact, DateOnly)>(ordered.Count);
        foreach (var fact in ordered) entries.Add((fact, Calendar.LocalDate(fact.OccurredAt, zone)));
        return entries;
    }

    private static long HighestSeq(IEnumerable<(Fact Fact, DateOnly Date)> entries, DateOnly? through) =>
        through is { } last ? entries.Where(e => e.Date <= last).Select(e => e.Fact.Seq).DefaultIfEmpty(0).Max() : 0;

    /// <summary>Takes a fact's effect into the state: plans, marks (with their deadline), logs, ratings, retros.</summary>
    private static void Ingest(FoldState state, (Fact Fact, DateOnly Date) entry)
    {
        var (fact, date) = entry;
        switch (fact)
        {
            case Fact.PlanApproved plan:
                AddPlan(state, state.StartDate, plan.Data.Curriculum);
                break;
            case Fact.RevisionApproved revision:
                // A revision governs from the day after the day it was approved.
                AddPlan(state, date.AddDays(1), revision.Data.Curriculum);
                break;
            case Fact.ActivityLogged logged:
                if (!state.DayLogs.TryGetValue(date, out var day)) state.DayLogs[date] = day = [];
                day[logged.Data.ActivityId] = day.GetValueOrDefault(logged.Data.ActivityId) + logged.Data.Amount;
                break;
            case Fact.DayMarkedOffline mark:
                Mark(state, fact.OccurredAt, mark.Data.Date, marked: true);
                break;
            case Fact.DayUnmarkedOffline unmark:
                Mark(state, fact.OccurredAt, unmark.Data.Date, marked: false);
                break;
            case Fact.DebtRated rated:
                state.PendingRatings.Add(rated.Data);
                break;
            // Retro facts mean something only once the retrospective has begun; while running they are ignored (ingest
            // runs before the fact's day is closed, so the phase is the one in force that day).
            case Fact.RetroProlonged prolonged when state.Phase == GoalPhase.Retrospective:
                state.Controls.Add(new FoldState.ControlEvent.Prolonged(date, prolonged.Data.ExtraActiveDays));
                break;
            case Fact.RetroEnded when state.Phase == GoalPhase.Retrospective:
                state.Controls.Add(new FoldState.ControlEvent.Ended(date));
                break;
            case Fact.RetroProlonged or Fact.RetroEnded or Fact.GoalCreated or Fact.SettingsChanged:
                break;
        }
    }

    private static void Mark(FoldState state, DateTimeOffset occurredAt, DateOnly target, bool marked)
    {
        // Only a mark or unmark made before the day's local midnight counts.
        if (!Calendar.IsBeforeLocalMidnight(occurredAt, target, state.Zone)) return;
        var i = state.PendingMarks.FindIndex(m => m.Date == target);
        if (i < 0) state.PendingMarks.Add(new FoldCarry.PendingMark(target, marked));
        else state.PendingMarks[i] = state.PendingMarks[i] with { Marked = marked };
    }

    /// <summary>Inserts after every plan from the same day or earlier, so of two plans from one day the later fact governs.</summary>
    private static void AddPlan(FoldState state, DateOnly effectiveFrom, Curriculum curriculum)
    {
        var at = state.Plans.FindIndex(p => p.EffectiveFrom > effectiveFrom);
        state.Plans.Insert(at < 0 ? state.Plans.Count : at, new FoldCarry.Plan(effectiveFrom, curriculum));
    }

    private static FoldCarry.Plan? PlanAt(FoldState state, DateOnly date) => state.Plans.LastOrDefault(p => p.EffectiveFrom <= date);

    /// <summary>What is due on <paramref name="date"/> (active-day number <paramref name="k"/>), floors scaled by each area's replan scale.</summary>
    private static List<Classify.DueFloor> DueOn(FoldState state, DateOnly date, int k)
    {
        var floors = new List<Classify.DueFloor>();
        if (PlanAt(state, date) is not { } plan) return floors;
        foreach (var d in Schedule.DueActivities(plan.Curriculum, k, date))
        {
            var scale = 1.0;
            foreach (var area in state.Areas)
            {
                if (area.AreaId != d.AreaId) continue;
                scale = area.ReplanScale;
                break;
            }
            floors.Add(new Classify.DueFloor(d.AreaId, d.Activity.Id, Schedule.ScaledFloor(d.Activity.Floor, scale)));
        }
        return floors;
    }

    /// <summary>The index of <paramref name="date"/>'s month in the usage, adding it at 0 on first sight (the view lists it then).</summary>
    private static int MonthEntry(FoldState state, DateOnly date)
    {
        var month = Calendar.MonthKey(date);
        for (var i = 0; i < state.MonthUsage.Count; i++)
        {
            if (state.MonthUsage[i].Month == month) return i;
        }
        state.MonthUsage.Add(new MonthUsage(month, 0));
        return state.MonthUsage.Count - 1;
    }

    private static bool HasRoom(FoldState state, DateOnly date) => Calendar.HasAllowanceRoom(state.OfflinePerMonthCap, state.MonthUsage, date);

    /// <summary>Classifies a closed day and updates the counters; returns what the debt step needs of the day.</summary>
    private static FoldState.ClosedDay CloseDay(FoldState state, DateOnly date)
    {
        var logged = state.DayLogs.GetValueOrDefault(date);
        var closed = new FoldState.ClosedDay(date, DayStatus.Retrospective, 0, [], logged);
        if (state.Phase == GoalPhase.Retrospective)
        {
            state.Days.Add(new GoalStateView.GoalDay(date, DayStatus.Retrospective, null));
            state.RetrospectiveDays++;
        }
        else
        {
            var month = MonthEntry(state, date);
            var k = state.ActiveDaysUsed;
            var due = DueOn(state, date, k);
            var marked = state.PendingMarks.FirstOrDefault(m => m.Date == date)?.Marked ?? false;
            // MonthEntry just made the month's entry, so the room is read from it, not looked up again by key.
            var hasRoom = state.MonthUsage[month].Used < Calendar.AllowanceCap(date, state.OfflinePerMonthCap);
            var status = Classify.Day(marked, hasRoom, logged, due);
            closed = new FoldState.ClosedDay(date, status, k, due, logged);
            if (status is DayStatus.Offline or DayStatus.Skipped)
            {
                state.MonthUsage[month] = state.MonthUsage[month] with { Used = state.MonthUsage[month].Used + 1 };
                if (status == DayStatus.Offline) state.OfflineUsed++;
                else state.SkippedUsed++;
                state.Days.Add(new GoalStateView.GoalDay(date, status, null));
            }
            else
            {
                state.ActiveDaysUsed++;
                state.Days.Add(new GoalStateView.GoalDay(date, status, k));
            }
            if (state.ActiveDaysUsed >= state.RangeActiveDays + state.ProlongedDays) state.Phase = GoalPhase.Retrospective;
        }
        state.PendingMarks.RemoveAll(m => m.Date <= date);
        state.DayLogs.Remove(date);
        state.LastClosedDate = date;
        return closed;
    }

    /// <summary>
    /// Bankruptcy, after a closed day: an area whose outstanding debt is over <c>BankruptcyFloorRatio</c> times the floors
    /// of all its activities in the day's milestone (any cadence, at the current scale, summed as raw numbers across
    /// units) is cleared: debt gone, multiplier back to base, floors from the next day scaled by
    /// <c>BankruptcyReplanScale</c>, and the bankruptcy recorded. An area past its last milestone has no yardstick, so it
    /// cannot go bankrupt.
    /// </summary>
    private static void ApplyBankruptcies(FoldState state, FoldState.ClosedDay day)
    {
        if (day.Status == DayStatus.Retrospective) return;
        var rules = state.Rules;
        if (PlanAt(state, day.Date) is not { } plan) return;
        for (var i = 0; i < state.Areas.Count; i++)
        {
            var area = state.Areas[i];
            var curriculumArea = plan.Curriculum.Areas.FirstOrDefault(a => a.Id == area.AreaId);
            if (curriculumArea is null || Schedule.MilestoneAt(curriculumArea, day.K) is not { } milestone) continue;
            var floors = milestone.Activities.Sum(a => Schedule.ScaledFloor(a.Floor, area.ReplanScale));
            var owed = state.Debt.Where(d => d.AreaId == area.AreaId).Sum(d => d.Amount);
            if (owed <= rules.BankruptcyFloorRatio * floors + Debt.Epsilon) continue;
            state.Debt.RemoveAll(d => d.AreaId == area.AreaId);
            // A bankruptcy also ends the escalation run: the next short day starts again from base (Ruling H).
            state.Areas[i] = area with
            {
                Multiplier = area.Base,
                NewDebtYesterday = false,
                ReplanScale = Debt.Round(area.ReplanScale * rules.BankruptcyReplanScale),
            };
            state.Bankruptcies.Add(new GoalStateView.Bankruptcy(area.AreaId, day.Date));
        }
    }

    /// <summary>Applies retro facts dated at or before <paramref name="date"/>: they take effect from the next day.</summary>
    private static void ApplyControls(FoldState state, DateOnly date)
    {
        var due = state.Controls.Where(c => c.Date <= date).ToList();
        state.Controls.RemoveAll(c => c.Date <= date);
        foreach (var control in due)
        {
            if (state.Phase == GoalPhase.Ended) break;
            switch (control)
            {
                case FoldState.ControlEvent.Ended:
                    state.Phase = GoalPhase.Ended;
                    break;
                case FoldState.ControlEvent.Prolonged prolonged:
                    state.ProlongedDays += prolonged.ExtraActiveDays;
                    if (state.Phase == GoalPhase.Retrospective && state.ActiveDaysUsed < state.RangeActiveDays + state.ProlongedDays)
                        state.Phase = GoalPhase.Running;
                    break;
            }
        }
    }

    private static GoalStateView BuildView(FoldState state, DateOnly today, DateOnly start, FoldState open)
    {
        var total = state.RangeActiveDays + state.ProlongedDays;
        // The end date is the last active day: retrospective days push it out only once a prolong has resumed the run,
        // so the stretch of retrospective days still going on is left out (Ruling F).
        var retrospectiveBefore = state.RetrospectiveDays;
        if (state.Phase != GoalPhase.Running)
        {
            for (var i = state.Days.Count - 1; i >= 0 && state.Days[i].Status == DayStatus.Retrospective; i--) retrospectiveBefore--;
        }
        var lengthInDays = total + state.OfflineUsed + state.SkippedUsed + retrospectiveBefore;
        var started = today >= start;
        // The same test the day's classification applies when it closes: a marked day with room is offline (Ruling AA).
        var offlineToday = state.Phase == GoalPhase.Running && started
            && open.PendingMarks.Any(m => m.Date == today && m.Marked) && HasRoom(state, today);
        IReadOnlyList<GoalStateView.RequiredActivity> required = state.Phase == GoalPhase.Running && started && !offlineToday
            ? Debt.RequiredToday(open, DueOn(open, today, state.ActiveDaysUsed))
            : [];
        var carry = state.ToCarry();
        return new GoalStateView(
            state.Phase,
            today,
            start.AddDays(lengthInDays - 1),
            state.ActiveDaysUsed,
            total,
            [.. state.Days],
            required,
            offlineToday,
            [.. open.PendingMarks.Where(m => m.Marked && m.Date >= today).Select(m => m.Date).Order()],
            Debt.View(state),
            [.. state.Areas.Select(a => new GoalStateView.AreaSummary(a.AreaId, a.Multiplier, a.Base, a.ReplanScale))],
            [.. state.MonthUsage.Select(m => new GoalStateView.MonthAllowance(m.Month, m.Used,
                Calendar.AllowanceCap(DateOnly.ParseExact(m.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture), state.OfflinePerMonthCap)))],
            [.. state.Bankruptcies],
            state.LastSeq,
            carry);
    }
}
