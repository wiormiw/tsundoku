using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

/// <summary>
/// An enum on the wire: exactly its camelCase name, as the TS picklists are. The BCL's string enum converter also
/// accepts any casing, which the contract does not.
/// </summary>
public sealed class WireEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    private static readonly Dictionary<string, T> ByName = Enum.GetValues<T>().ToDictionary(Name, StringComparer.Ordinal);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && ByName.TryGetValue(reader.GetString()!, out var value)
            ? value
            : throw new JsonException($"not a {typeof(T).Name}: one of {string.Join(", ", ByName.Keys)}");

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(Name(value));
    }

    private static string Name(T value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
