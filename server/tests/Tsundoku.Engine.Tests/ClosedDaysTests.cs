namespace Tsundoku.Engine.Tests;

/// <summary>
/// <c>OnDayClosed</c> hands over the state a fold ending the next day gives (the TS <c>closed-days.test.ts</c>), and a fold
/// resumed from that state equals the full fold. Views are compared on the wire, which is also their deep equality.
/// </summary>
public sealed class ClosedDaysTests
{
    public static TheoryData<string> Files => [.. Scenarios.Files];

    private static (IReadOnlyList<Fact> Facts, Rules Rules, DateOnly Last) Load(string file)
    {
        var scenario = Scenarios.Load(file);
        return (Scenarios.ExpandFacts(scenario), Scenarios.RulesOf(scenario), scenario.Checks.Max(c => c.Today));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void OnDayClosed_hands_over_the_state_a_fold_ending_the_next_day_gives(string file)
    {
        var (facts, rules, today) = Load(file);
        var seen = new List<GoalStateView>();
        var final = Fold.FoldGoal(new FoldInput(facts, today, rules, OnDayClosed: seen.Add));

        // The hook does not change the result.
        Scenarios.Text(final).ShouldBe(Scenarios.Text(Fold.FoldGoal(new FoldInput(facts, today, rules))));
        seen.Select(s => s.Carry.LastClosedDate).ShouldBe(final.Days.Select(d => (DateOnly?)d.Date));
        foreach (var view in seen)
        {
            var closed = view.Carry.LastClosedDate!.Value;
            Scenarios.Text(view).ShouldBe(Scenarios.Text(Fold.FoldGoal(new FoldInput(facts, closed.AddDays(1), rules))), $"closed {closed}");
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Resuming_from_any_closed_day_equals_the_full_fold(string file)
    {
        var (facts, rules, today) = Load(file);
        var timezone = facts.OfType<Fact.GoalCreated>().Single().Data.Timezone;
        var full = Scenarios.Text(Fold.FoldGoal(new FoldInput(facts, today, rules)));
        var snapshots = new List<GoalStateView>();
        Fold.FoldGoal(new FoldInput(facts, today, rules, OnDayClosed: snapshots.Add));
        foreach (var snapshot in snapshots)
        {
            var closed = snapshot.Carry.LastClosedDate!.Value;
            var rest = facts.Where(f => Calendar.LocalDate(f.OccurredAt, timezone) > closed).ToList();
            Scenarios.Text(Fold.FoldGoal(new FoldInput(rest, today, rules, snapshot))).ShouldBe(full, $"resumed after {closed}");
        }
    }

    [Fact]
    public void A_late_fact_or_other_rules_refuse_the_snapshot()
    {
        var (facts, rules, today) = Load("debt-shortfall-then-payment.json");
        var snapshots = new List<GoalStateView>();
        Fold.FoldGoal(new FoldInput(facts, today, rules, OnDayClosed: snapshots.Add));
        var snapshot = snapshots[^1];

        Should.Throw<ArgumentException>(() => Fold.FoldGoal(new FoldInput(facts, today, rules, snapshot)));
        Should.Throw<ArgumentException>(() => Fold.FoldGoal(new FoldInput([], today, rules with { BaseMax = 1.5 }, snapshot)));
    }

    [Fact]
    public void The_views_handed_over_are_copies_later_days_do_not_rewrite_earlier_ones()
    {
        var (facts, rules, _) = Load("debt-shortfall-then-payment.json");
        var seen = new List<GoalStateView>();
        Fold.FoldGoal(new FoldInput(facts, new DateOnly(2026, 3, 6), rules, OnDayClosed: seen.Add));
        seen.Count.ShouldBeGreaterThan(2);
        seen[0].Days.Count.ShouldBe(1);
        seen[0].Carry.LastSeq.ShouldNotBe(seen[^1].Carry.LastSeq);
    }

    [Fact]
    public void The_scenarios_close_more_than_50_days_between_them() =>
        Scenarios.Files.Select(Load).Sum(s => Fold.FoldGoal(new FoldInput(s.Facts, s.Last, s.Rules)).Days.Count).ShouldBeGreaterThan(50);
}
