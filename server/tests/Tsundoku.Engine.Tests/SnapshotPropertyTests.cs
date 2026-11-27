namespace Tsundoku.Engine.Tests;

/// <summary>
/// The TS <c>snapshot.property.test.ts</c>, on generated logs: folding from the snapshot of any closed day equals the
/// full fold, for every later today, and never mutates the snapshot. A seeded <see cref="Random"/> keeps it repeatable.
/// </summary>
public sealed class SnapshotPropertyTests
{
    private static readonly string[] Zones = ["Asia/Jakarta", "America/Santiago", "Pacific/Auckland", "UTC"];

    private static Guid Id(int n) => new($"00000000-0000-4000-8000-{n:D12}");

    private static List<Fact> Generate(Random random, out string timezone, out DateOnly last, out Rules rules)
    {
        timezone = Zones[random.Next(Zones.Length)];
        var start = new DateOnly(2026, 1, 1).AddDays(random.Next(400));
        var areaCount = random.Next(1, 4);
        var areaIds = Enumerable.Range(0, areaCount).Select(i => Id(0xa0 + i)).ToList();
        var activityIds = new List<Guid>();
        var areas = areaIds.Select((area, i) =>
        {
            var activities = Enumerable.Range(0, random.Next(1, 3)).Select(j =>
            {
                var id = Id(0xb00 + i * 10 + j);
                activityIds.Add(id);
                var floor = random.Next(1, 40);
                return new Activity(id, "Act", Unit.Reps, floor, floor + random.Next(0, 20), new Cadence.Daily());
            }).ToList();
            return new Area(area, "A", [new Milestone(Id(0xc00 + i), "M", 60, activities)]);
        }).ToList();

        var span = random.Next(5, 25);
        var instant = new DateTimeOffset(start.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var seq = 2L;
        var goal = Id(0xf001);
        var facts = new List<Fact>
        {
            new Fact.GoalCreated(new("G", [.. areaIds.Select(a => new Fact.GoalCreated.AreaName(a, "A"))], 30, start, timezone, new Fact.GoalCreated.Caps(random.Next(0, 3))))
                { Id = Id(1), GoalId = goal, Seq = 1, V = 1, OccurredAt = instant, ReceivedAt = instant },
            new Fact.PlanApproved(new(1, new Curriculum(areas)))
                { Id = Id(2), GoalId = goal, Seq = 2, V = 1, OccurredAt = instant.AddMinutes(1), ReceivedAt = instant.AddMinutes(1) },
        };
        // Facts move forward in time and seq, as a synced log does; some days get several logs, some none.
        var at = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(-14);
        for (var step = 0; step < span * 3; step++)
        {
            at = at.AddMinutes(random.Next(60, 900));
            var pick = random.Next(10);
            Fact fact = pick switch
            {
                < 7 => new Fact.ActivityLogged(new(activityIds[random.Next(activityIds.Count)], random.Next(0, 90) / 2.0))
                    { Id = Id((int)++seq), GoalId = goal, Seq = seq, V = 1, OccurredAt = at, ReceivedAt = at },
                _ => new Fact.DayMarkedOffline(new Fact.DayPayload(Calendar.LocalDate(at, timezone).AddDays(random.Next(0, 3))))
                    { Id = Id((int)++seq), GoalId = goal, Seq = seq, V = 1, OccurredAt = at, ReceivedAt = at },
            };
            facts.Add(fact);
        }
        last = Calendar.LocalDate(at, timezone).AddDays(random.Next(0, 3));
        rules = Rules.Default;
        return facts;
    }

    [Fact]
    public void Resuming_from_any_closed_day_equals_the_full_fold_on_generated_logs()
    {
        var random = new Random(20261127);
        var resumes = 0;
        for (var run = 0; run < 60; run++)
        {
            var facts = Generate(random, out var timezone, out var last, out var rules);
            var closedDays = Fold.FoldGoal(new FoldInput(facts, last, rules)).Days.Select(d => d.Date).ToList();
            foreach (var closed in closedDays)
            {
                var snapshot = Fold.FoldGoal(new FoldInput(facts, closed.AddDays(1), rules));
                var frozen = Scenarios.Text(snapshot);
                var rest = facts.Where(f => Calendar.LocalDate(f.OccurredAt, timezone) > closed).ToList();
                for (var today = closed.AddDays(1); today <= last; today = today.AddDays(1))
                {
                    var full = Scenarios.Text(Fold.FoldGoal(new FoldInput(facts, today, rules)));
                    Scenarios.Text(Fold.FoldGoal(new FoldInput(rest, today, rules, snapshot))).ShouldBe(full, $"run {run} ({timezone}): resume from {closed} to {today}");
                    resumes++;
                }
                Scenarios.Text(snapshot).ShouldBe(frozen, "the snapshot is not mutated");
            }
        }
        resumes.ShouldBeGreaterThan(500);
    }
}
