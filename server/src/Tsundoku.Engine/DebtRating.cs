using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

[JsonConverter(typeof(WireEnumConverter<DebtRating>))]
public enum DebtRating
{
    Easy,
    Right,
    Hard,
}
