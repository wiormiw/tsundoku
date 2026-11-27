using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

[JsonConverter(typeof(WireEnumConverter<DayStatus>))]
public enum DayStatus
{
    Met,
#pragma warning disable CA1720 // The wire name of a day short of its floors.
    Short,
#pragma warning restore CA1720
    Offline,
    Skipped,
    Missed,
    Retrospective,
}
