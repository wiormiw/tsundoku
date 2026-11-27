namespace Tsundoku.Engine.Tests;

/// <summary>
/// Fold order is occurredAt, then seq (the TS sort, stable). A table's seq defaults to the fact's position, so it is
/// unique and the order total: however the facts arrive, the fold is the same.
/// </summary>
public sealed class FactOrderTests
{
    public static TheoryData<string> Files => [.. Scenarios.Files];

    [Theory]
    [MemberData(nameof(Files))]
    public void Arrival_order_does_not_change_the_fold(string file)
    {
        var scenario = Scenarios.Load(file);
        var facts = Scenarios.ExpandFacts(scenario);
        var rules = Scenarios.RulesOf(scenario);
        var today = scenario.Checks.Max(c => c.Today);
        var expected = Scenarios.Text(Fold.FoldGoal(new FoldInput(facts, today, rules)));

        Scenarios.Text(Fold.FoldGoal(new FoldInput([.. facts.Reverse()], today, rules))).ShouldBe(expected, "reversed");
        var shuffled = facts.ToList();
        new Random(20261127).Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(shuffled));
        Scenarios.Text(Fold.FoldGoal(new FoldInput(shuffled, today, rules))).ShouldBe(expected, "shuffled");
    }

    [Fact]
    public void Facts_at_one_instant_fold_by_seq_not_by_arrival()
    {
        var scenario = Scenarios.Load("offline-last-mark-wins-and-seq-breaks-ties.json");
        var facts = Scenarios.ExpandFacts(scenario);
        var rules = Scenarios.RulesOf(scenario);
        var tied = facts.GroupBy(f => f.OccurredAt).Count(g => g.Count() > 1);
        tied.ShouldBeGreaterThan(0, "the table has facts sharing an instant");
        foreach (var check in scenario.Checks)
        {
            var viaSeqDescending = facts.OrderByDescending(f => f.Seq).ToList();
            Scenarios.Text(Fold.FoldGoal(new FoldInput(viaSeqDescending, check.Today, rules)))
                .ShouldBe(Scenarios.Text(Fold.FoldGoal(new FoldInput(facts, check.Today, rules))));
        }
    }
}
