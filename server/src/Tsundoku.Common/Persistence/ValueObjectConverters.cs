using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tsundoku.Common.Domain;

namespace Tsundoku.Common.Persistence;

/// <summary>
/// Conversions for the types that every module shares. A module adds its own ids and value objects in its context.
/// The database holds only values that the domain accepted, so a value that the domain refuses on read is a bug.
/// </summary>
internal static class ValueObjectConverters
{
    public static void Register(ModelConfigurationBuilder builder)
    {
        builder.Properties<UserId>().HaveConversion<UserIdConverter>().HaveMaxLength(UserId.MaxLength);
        // Postgres keeps timestamptz as UTC, and Npgsql refuses other offsets.
        builder.Properties<DateTimeOffset>().HaveConversion<UtcConverter>();
    }

    private sealed class UserIdConverter() : ValueConverter<UserId, string>(id => id.Value, value => new UserId(value));

    private sealed class UtcConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(value => value.ToUniversalTime(), value => value);
}
