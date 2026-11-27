using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

[JsonConverter(typeof(WireEnumConverter<GoalPhase>))]
public enum GoalPhase
{
    Running,
    Retrospective,
    Ended,
}
