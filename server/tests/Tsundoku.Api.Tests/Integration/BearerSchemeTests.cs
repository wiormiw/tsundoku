using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Tsundoku.Api.Tests.Integration;

public sealed class BearerSchemeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_garbage_bearer_token_is_401()
    {
        using var client = factory.CreateApiClient(user: null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "abc");

        (await client.GetAsync("/api/todo-lists", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_test_header_still_signs_in()
    {
        using var client = factory.CreateApiClient("alice");

        (await client.GetAsync("/api/todo-lists", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void Startup_refuses_an_unknown_default_time_zone()
    {
        Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
        using var broken = factory.WithWebHostBuilder(host => host.UseSetting("Accounts:DefaultTimeZone", "Mars/Base"));

        Should.Throw<OptionsValidationException>(() => broken.CreateClient());
    }
}
