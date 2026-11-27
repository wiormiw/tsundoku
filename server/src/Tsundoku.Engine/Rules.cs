using System.Globalization;
using System.Reflection;

namespace Tsundoku.Engine;

/// <summary>The domain tunables of the engine. Each default is here and only here; the API overrides them from config.</summary>
public sealed record Rules
{
    public static Rules Default { get; } = new();

    /// <summary>Starting debt multiplier of an area, and what it returns to when its debt is paid.</summary>
    public double BaseMultiplier { get; init; } = 1.25;

    /// <summary>Active days a debt item has before it is flagged overdue.</summary>
    public int DueWindowDays { get; init; } = 2;

    /// <summary>A day never requires more than this share of a floor (floor plus debt).</summary>
    public double DailyCapRatio { get; init; } = 1.5;

    /// <summary>Added to an area's multiplier when it creates new debt on consecutive days.</summary>
    public double EscalationStep { get; init; } = 0.1;

    public double MultiplierMax { get; init; } = 2.0;

    /// <summary>Self-tuning keeps an area's base within [BaseMin, BaseMax].</summary>
    public double BaseMin { get; init; } = 1.1;

    public double BaseMax { get; init; } = 1.75;

    public double TuneStep { get; init; } = 0.05;

    /// <summary>Bankruptcy when outstanding debt exceeds this many times the area's total daily floor.</summary>
    public double BankruptcyFloorRatio { get; init; } = 3;

    /// <summary>What the area's floors are multiplied by on each bankruptcy.</summary>
    public double BankruptcyReplanScale { get; init; } = 0.8;

    private static readonly PropertyInfo[] Fields = [.. typeof(Rules)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .OrderBy(property => WireName(property), StringComparer.Ordinal)];

    /// <summary>
    /// The rules as text, keys sorted: a snapshot stores it so a fold can tell it was computed under other rules.
    /// Byte for byte the TS <c>rulesKey</c>, so the keys are the camelCase wire names and numbers print shortest round-trip, like JS.
    /// </summary>
    public static string Key(Rules rules) => string.Join(';', Fields.Select(property =>
        WireName(property) + "=" + Convert.ToString(property.GetValue(rules), CultureInfo.InvariantCulture)));

    private static string WireName(PropertyInfo property) => char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
}
