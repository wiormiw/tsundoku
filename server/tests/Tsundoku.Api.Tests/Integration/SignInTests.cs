using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Tsundoku.Api.Tests.Integration.AccountsClient;

namespace Tsundoku.Api.Tests.Integration;

public sealed class SignInTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string?> Code(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Ct))?["code"]?.ToString();

    [Fact]
    public async Task A_confirmed_user_gets_tokens_and_reads_a_protected_route()
    {
        var (email, password) = await SignUpConfirmedAsync(factory, Ct);
        using var client = factory.CreateApiClient(user: null);

        var tokens = await SignInAsync(client, email, password, Ct);

        tokens.TokenType.ShouldBe("Bearer");
        tokens.RefreshToken.ShouldNotBeNullOrEmpty();
        (await GetLists(client, tokens.AccessToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unconfirmed_with_the_right_password_is_403()
    {
        using var client = factory.CreateApiClient(user: null);
        var email = await SignUpAsync(client, Ct);

        var response = await SignIn(client, email, Password, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Code(response)).ShouldBe("accounts.email_not_confirmed");
    }

    [Fact]
    public async Task Unknown_email_wrong_password_and_unconfirmed_wrong_password_are_identical_401s()
    {
        var (confirmed, _) = await SignUpConfirmedAsync(factory, Ct);
        using var client = factory.CreateApiClient(user: null);
        var unconfirmed = await SignUpAsync(client, Ct);

        var unknown = await SignIn(client, NewEmail(), Password, Ct);
        var wrong = await SignIn(client, confirmed, "not the password at all", Ct);
        var unconfirmedWrong = await SignIn(client, unconfirmed, "not the password at all", Ct);

        var body = await ProblemBody(unknown, Ct);
        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Code(unknown)).ShouldBe("accounts.invalid_credentials");
        foreach (var response in new[] { wrong, unconfirmedWrong })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await ProblemBody(response, Ct)).ShouldBe(body);
        }
    }

    [Fact]
    public async Task Lockout_after_max_attempts_answers_401_even_with_the_right_password()
    {
        using var strict = factory.WithWebHostBuilder(host => host.UseSetting("Accounts:LockoutMaxFailedAttempts", "3"));
        var (email, password) = await SignUpConfirmedAsync(factory, Ct);
        using var client = strict.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
            (await SignIn(client, email, "not the password at all", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var locked = await SignIn(client, email, password, Ct);

        locked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Code(locked)).ShouldBe("accounts.invalid_credentials");
    }

    [Fact]
    public async Task An_unconfirmed_account_locks_out_too()
    {
        using var strict = factory.WithWebHostBuilder(host => host.UseSetting("Accounts:LockoutMaxFailedAttempts", "3"));
        using var client = strict.CreateClient();
        var email = await SignUpAsync(client, Ct);

        for (var attempt = 0; attempt < 3; attempt++)
            (await SignIn(client, email, "not the password at all", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The 403 would confirm the guess; a locked account answers 401 instead.
        (await SignIn(client, email, Password, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_session_row_keeps_ip_and_user_agent()
    {
        using var fromAddress = factory.WithWebHostBuilder(host => host.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new RemoteAddress(IPAddress.Parse("203.0.113.7")))));
        var (email, password) = await SignUpConfirmedAsync(factory, Ct);
        using var client = fromAddress.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("tsundoku-tests/1.0");

        await SignInAsync(client, email, password, Ct);

        await using var connection = new NpgsqlConnection(factory.OwnerConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "select s.ip_address, s.user_agent from accounts.sessions s join accounts.users u on u.id = s.user_id where u.email = @email", connection);
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        (await reader.ReadAsync(Ct)).ShouldBeTrue();
        reader.GetString(0).ShouldBe("203.0.113.7");
        reader.GetString(1).ShouldBe("tsundoku-tests/1.0");
    }

    [Fact]
    public async Task The_token_response_is_no_store()
    {
        var (email, password) = await SignUpConfirmedAsync(factory, Ct);
        using var client = factory.CreateApiClient(user: null);

        var response = await SignIn(client, email, password, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    /// <summary>The test server has no remote address; this gives every request one, the way Kestrel would.</summary>
    private sealed class RemoteAddress(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((HttpContext context, RequestDelegate following) =>
            {
                context.Connection.RemoteIpAddress = address;
                return following(context);
            });
            next(app);
        };
    }
}
