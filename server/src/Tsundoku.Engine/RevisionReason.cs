using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

[JsonConverter(typeof(WireEnumConverter<RevisionReason>))]
public enum RevisionReason
{
    Requested,
    Bankruptcy,
    Prolong,
}
