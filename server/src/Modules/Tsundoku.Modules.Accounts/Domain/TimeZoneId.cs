using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Modules.Accounts.Domain;

/// <summary>An IANA time zone id (<c>Asia/Jakarta</c>). Windows ids and offsets are refused, so clients on any platform agree.</summary>
internal sealed record TimeZoneId
{
    private TimeZoneId(string value) => Value = value;

    public string Value { get; }

    public static Result<TimeZoneId> Create(string? id) =>
        id is { Length: > 0 } && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) && zone.HasIanaId
            ? new TimeZoneId(id)
            : AccountErrors.TimeZoneUnknown;

    /// <summary>For persistence only: the database holds only values that <see cref="Create"/> accepted.</summary>
    internal static TimeZoneId FromTrusted(string value) => new(value);

    public override string ToString() => Value;
}
