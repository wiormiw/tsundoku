using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tsundoku.Engine.Tests;

/// <summary>A fold scenario table: the TS <c>FoldScenario</c>, read as strictly as valibot's <c>strictObject</c>.</summary>
public sealed record Scenario(string Name, IReadOnlyList<ScenarioFact> Facts, IReadOnlyList<ScenarioCheck> Checks, IReadOnlyDictionary<string, double>? Rules = null);

/// <summary>A fact without the envelope fields a table has no use for; <see cref="Scenarios.ExpandFacts"/> fills them in.</summary>
public sealed record ScenarioFact(string Type, string OccurredAt, JsonElement Data, string? Seq = null);

public sealed record ScenarioCheck(DateOnly Today, JsonObject Expect);

/// <summary>The tables in <c>fixtures/engine</c>, shared with the Bun engine and the clients; read in place, never edited.</summary>
public static class Scenarios
{
    public static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private const string GoalId = "00000000-0000-4000-8000-00000000f001";

    public static string Folder { get; } = FindFolder();

    public static IReadOnlyList<string> Files { get; } =
        [.. Directory.EnumerateFiles(Folder, "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal).Cast<string>()];

    public static Scenario Load(string file) =>
        JsonSerializer.Deserialize<Scenario>(File.ReadAllText(Path.Combine(Folder, file)), Strict)
        ?? throw new InvalidDataException($"{file}: empty");

    /// <summary>As the TS <c>expandFacts</c>: ids from the position, seq defaulting to it (from 1), received when it occurred.</summary>
    public static IReadOnlyList<Fact> ExpandFacts(Scenario scenario) =>
        [.. scenario.Facts.Select((fact, index) =>
        {
            var envelope = new JsonObject
            {
                // The discriminator first: System.Text.Json reads it only there.
                ["type"] = fact.Type,
                ["id"] = $"00000000-0000-4000-8000-{(index + 1).ToString("D12", CultureInfo.InvariantCulture)}",
                ["goalId"] = GoalId,
                ["seq"] = fact.Seq ?? (index + 1).ToString(CultureInfo.InvariantCulture),
                ["v"] = 1,
                ["occurredAt"] = fact.OccurredAt,
                ["receivedAt"] = fact.OccurredAt,
                ["data"] = JsonNode.Parse(fact.Data.GetRawText()),
            };
            return envelope.Deserialize<Fact>(Strict) ?? throw new InvalidDataException($"{scenario.Name}: fact {index + 1} is null");
        })];

    /// <summary>The default rules with the table's overrides; an unknown rule name fails.</summary>
    public static Rules RulesOf(Scenario scenario)
    {
        var rules = JsonSerializer.SerializeToNode(Rules.Default, Strict)!.AsObject();
        foreach (var (name, value) in scenario.Rules ?? new Dictionary<string, double>())
        {
            if (!rules.ContainsKey(name)) throw new InvalidDataException($"{scenario.Name}: unknown rule {name}");
            rules[name] = value;
        }
        return rules.Deserialize<Rules>(Strict)!;
    }

    /// <summary>The view as wire JSON: what the TS runner compares, and a deep equality for records holding lists.</summary>
    public static JsonNode Wire(GoalStateView view) => JsonNode.Parse(JsonSerializer.Serialize(view, Strict))!;

    public static string Text(GoalStateView view) => JsonSerializer.Serialize(view, Strict);

    private static string FindFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName, "fixtures", "engine");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("fixtures/engine above " + AppContext.BaseDirectory);
    }
}
