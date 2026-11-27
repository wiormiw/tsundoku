using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

[JsonConverter(typeof(WireEnumConverter<Unit>))]
public enum Unit
{
    Reps,
    Minutes,
    Pages,
    Sessions,
    Count,
    Done,
}
