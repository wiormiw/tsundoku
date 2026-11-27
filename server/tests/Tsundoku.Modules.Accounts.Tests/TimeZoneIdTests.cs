using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Accounts.Domain;

namespace Tsundoku.Modules.Accounts.Tests;

public sealed class TimeZoneIdTests
{
    [Theory]
    [InlineData("Asia/Jakarta")]
    [InlineData("UTC")]
    [InlineData("America/Santiago")]
    public void An_iana_id_is_accepted(string id) => TimeZoneId.Create(id).Value.ShouldBeOfType<TimeZoneId>().Value.ShouldBe(id);

    [Theory]
    [InlineData("Mars/Base")]
    [InlineData("+07:00")]
    [InlineData("")]
    [InlineData("SE Asia Standard Time")]
    public void Anything_else_is_refused_on_the_timezone_field(string id) =>
        TimeZoneId.Create(id).Value.ShouldBeOfType<ValidationError>().Errors.ShouldContainKey("timezone");
}
