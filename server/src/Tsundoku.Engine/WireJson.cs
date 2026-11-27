using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

/// <summary>
/// The one set of options that reads and writes the engine's wire shapes (facts, snapshots). Strict: an unknown member
/// or a missing required one fails with a <see cref="JsonException"/>. Out-of-order metadata is allowed because
/// Postgres jsonb reorders keys, so <c>type</c> and <c>kind</c> need not come first.
/// </summary>
public static class WireJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowOutOfOrderMetadataProperties = true,
    };
}
