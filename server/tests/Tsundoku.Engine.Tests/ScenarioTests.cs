using System.Text.Json.Nodes;

namespace Tsundoku.Engine.Tests;

/// <summary>
/// Every table in <c>fixtures/engine</c>, folded at each check's <c>today</c> and compared as the TS runner
/// (<c>apps/api/test/engine/scenarios.test.ts</c>) compares: an expectation is a subset of the view, on the wire.
/// </summary>
public sealed class ScenarioTests
{
    public static TheoryData<string> Files => [.. Scenarios.Files];

    [Fact]
    public void There_are_the_34_tables() => Scenarios.Files.Count.ShouldBe(34);

    [Theory]
    [MemberData(nameof(Files))]
    public void The_fold_meets_every_check(string file)
    {
        var scenario = Scenarios.Load(file);
        file.ShouldBe($"{scenario.Name}.json");
        var facts = Scenarios.ExpandFacts(scenario);
        var rules = Scenarios.RulesOf(scenario);
        var failures = scenario.Checks.SelectMany(check =>
            Mismatches(check.Expect, Scenarios.Wire(Fold.FoldGoal(new FoldInput(facts, check.Today, rules))))
                .Select(diff => $"today {check.Today:yyyy-MM-dd}: {diff}"));
        failures.ShouldBeEmpty();
    }

    private static List<string> Mismatches(JsonObject expectation, JsonNode view)
    {
        var diffs = new List<string>();
        foreach (var (key, want) in expectation)
        {
            switch (key)
            {
                case "offlineToday" or "phase" or "endDate" or "activeDaysUsed" or "activeDaysTotal":
                    if (!JsonNode.DeepEquals(want, view[key])) diffs.Add($"{key}: want {want?.ToJsonString()}, got {view[key]?.ToJsonString()}");
                    break;
                case "days":
                    Keyed(diffs, key, "date", want!.AsArray(), view["days"]!.AsArray());
                    break;
                case "required":
                    Keyed(diffs, key, "activityId", want!.AsArray(), view["required"]!.AsArray());
                    break;
                case "allowance":
                    Keyed(diffs, key, "month", want!.AsArray(), view["allowance"]!.AsArray());
                    break;
                case "areas":
                    Keyed(diffs, key, "areaId", want!.AsArray(), view["areas"]!.AsArray());
                    break;
                case "requiredExactly":
                    var required = view["required"]!.AsArray();
                    if (want!.AsArray().Count != required.Count) diffs.Add($"{key}: want {want.AsArray().Count} entries, got {required.Count}");
                    Keyed(diffs, key, "activityId", want.AsArray(), required);
                    break;
                case "debtExactly":
                    var debt = view["debt"]!.AsArray();
                    var items = want!.AsArray();
                    if (items.Count != debt.Count) diffs.Add($"{key}: want {items.Count} items, got {debt.Count}");
                    for (var i = 0; i < Math.Min(items.Count, debt.Count); i++) Subset(diffs, $"{key}[{i}]", items[i]!.AsObject(), debt[i]!.AsObject());
                    break;
                case "daysCount":
                    if (want!.GetValue<int>() != view["days"]!.AsArray().Count) diffs.Add($"{key}: want {want}, got {view["days"]!.AsArray().Count}");
                    break;
                case "bankruptcies" or "markedOffline":
                    if (!JsonNode.DeepEquals(want, view[key])) diffs.Add($"{key}: want {want?.ToJsonString()}, got {view[key]?.ToJsonString()}");
                    break;
                case "absentDays":
                    foreach (var date in want!.AsArray())
                    {
                        if (view["days"]!.AsArray().Any(day => JsonNode.DeepEquals(day!["date"], date))) diffs.Add($"days[{date}]: should be absent");
                    }
                    break;
                default:
                    // The TS Expectation is a strictObject: an unknown key is a broken table, not a skipped check.
                    diffs.Add($"{key}: not an expectation");
                    break;
            }
        }
        return diffs;
    }

    private static void Keyed(List<string> diffs, string name, string key, JsonArray wanted, JsonArray actual)
    {
        foreach (var want in wanted.Select(entry => entry!.AsObject()))
        {
            var id = want[key];
            var got = actual.FirstOrDefault(entry => JsonNode.DeepEquals(entry![key], id));
            if (got is null) diffs.Add($"{name}[{id}]: missing");
            else Subset(diffs, $"{name}[{id}]", want, got.AsObject());
        }
    }

    /// <summary>Each key of <paramref name="want"/> holds the same value in <paramref name="got"/>; a key the view lacks fails.</summary>
    private static void Subset(List<string> diffs, string label, JsonObject want, JsonObject got)
    {
        foreach (var (key, value) in want)
        {
            if (!got.TryGetPropertyValue(key, out var actual) || !JsonNode.DeepEquals(value, actual))
                diffs.Add($"{label}.{key}: want {value?.ToJsonString()}, got {actual?.ToJsonString()}");
        }
    }
}
