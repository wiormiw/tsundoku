using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

/// <summary>When an activity is due: every day, or on ISO weekdays (1 = Monday .. 7 = Sunday).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Daily), "daily")]
[JsonDerivedType(typeof(Weekly), "weekly")]
public closed record Cadence
{
    public sealed record Daily : Cadence;

    public sealed record Weekly(IReadOnlyList<int> Weekdays) : Cadence;
}
