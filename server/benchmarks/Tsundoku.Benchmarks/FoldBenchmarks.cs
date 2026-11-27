using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Tsundoku.Engine;
using Tsundoku.Engine.Tests;

namespace Tsundoku.Benchmarks;

/// <summary>The fold on the largest scenario table and on a generated year of daily facts, from scratch and from a snapshot.</summary>
[MemoryDiagnoser]
public class FoldBenchmarks
{
    private const string Timezone = "Asia/Jakarta";
    private static Guid Id(int n) => new($"00000000-0000-4000-8000-{n:D12}");

    private FoldInput _fixture = null!;
    private FoldInput _yearFromStart = null!;
    private FoldInput _yearFromSnapshot = null!;
    private FoldInput _yearShuffled = null!;

    [GlobalSetup]
    public void Setup()
    {
        var largest = Scenarios.Files.Select(Scenarios.Load).MaxBy(s => s.Facts.Count)!;
        _fixture = new FoldInput(Scenarios.ExpandFacts(largest), largest.Checks.Max(c => c.Today), Scenarios.RulesOf(largest));

        var start = new DateOnly(2026, 1, 1);
        var today = start.AddDays(365);
        var facts = Year(start);
        _yearFromStart = new FoldInput(facts, today, Rules.Default);
        // The same year in arrival order scrambled: the case the already-ordered check cannot skip.
        var scrambled = facts.ToList();
        new Random(7).Shuffle(CollectionsMarshal.AsSpan(scrambled));
        _yearShuffled = new FoldInput(scrambled, today, Rules.Default);
        // Snapshot after day 300; the resumed fold holds only the later facts.
        var closed = start.AddDays(299);
        var snapshot = Fold.FoldGoal(new FoldInput(facts, closed.AddDays(1), Rules.Default));
        var rest = facts.Where(f => Calendar.LocalDate(f.OccurredAt, Timezone) > closed).ToList();
        _yearFromSnapshot = new FoldInput(rest, today, Rules.Default, snapshot);
    }

    /// <summary>One goal, one daily activity logged every day (a short day now and then), a few offline marks and revisions.</summary>
    private static List<Fact> Year(DateOnly start)
    {
        var random = new Random(20261127);
        var area = Id(0xa0);
        var activity = Id(0xb00);
        Curriculum Plan(int floor) => new([new Area(area, "A", [new Milestone(Id(0xc00), "M", 400, [new Activity(activity, "Act", Unit.Reps, floor, floor + 10, new Cadence.Daily())])])]);
        var instant = new DateTimeOffset(start.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var seq = 2L;
        var goal = Id(0xf001);
        var facts = new List<Fact>
        {
            new Fact.GoalCreated(new("G", [new Fact.GoalCreated.AreaName(area, "A")], 400, start, Timezone, new Fact.GoalCreated.Caps(null)))
                { Id = Id(1), GoalId = goal, Seq = 1, V = 1, OccurredAt = instant, ReceivedAt = instant },
            new Fact.PlanApproved(new(1, Plan(20)))
                { Id = Id(2), GoalId = goal, Seq = 2, V = 1, OccurredAt = instant.AddMinutes(1), ReceivedAt = instant.AddMinutes(1) },
        };
        for (var day = 0; day < 365; day++)
        {
            var at = new DateTimeOffset(start.AddDays(day).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).AddHours(8 + random.Next(10));
            facts.Add(new Fact.ActivityLogged(new(activity, random.Next(10, 40)))
                { Id = Id((int)++seq), GoalId = goal, Seq = seq, V = 1, OccurredAt = at, ReceivedAt = at });
            if (day % 90 == 45)
                facts.Add(new Fact.RevisionApproved(new(1, Plan(15 + day / 30), RevisionReason.Requested))
                    { Id = Id((int)++seq), GoalId = goal, Seq = seq, V = 1, OccurredAt = at.AddHours(1), ReceivedAt = at.AddHours(1) });
            if (day % 30 == 10)
                facts.Add(new Fact.DayMarkedOffline(new Fact.DayPayload(start.AddDays(day + 2)))
                    { Id = Id((int)++seq), GoalId = goal, Seq = seq, V = 1, OccurredAt = at.AddHours(2), ReceivedAt = at.AddHours(2) });
        }
        return facts;
    }

    [Benchmark] public GoalStateView LargestFixture() => Fold.FoldGoal(_fixture);

    [Benchmark] public GoalStateView YearFromStart() => Fold.FoldGoal(_yearFromStart);

    [Benchmark] public GoalStateView YearFromSnapshot() => Fold.FoldGoal(_yearFromSnapshot);

    /// <summary>The path production runs: a view handed over at every closed day.</summary>
    [Benchmark] public int YearOnDayClosed() { var n = 0; Fold.FoldGoal(_yearFromStart with { OnDayClosed = _ => n++ }); return n; }

    [Benchmark] public GoalStateView YearShuffled() => Fold.FoldGoal(_yearShuffled);
}
