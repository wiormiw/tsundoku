using System.Net;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Tsundoku.Modules.Accounts;
using static Tsundoku.Api.Tests.Integration.AccountsClient;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>Refresh and expiry, on a clock that the tests move. Tests of one class run one at a time, so moving it here is safe.</summary>
public sealed class SessionTests(ClockApiFactory factory) : IClassFixture<ClockApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AccountsOptions Options => factory.Services.GetRequiredService<IOptions<AccountsOptions>>().Value;

    private async Task<(HttpClient Client, string Email, AccessTokenResponse Tokens)> SignedIn()
    {
        var (email, password) = await SignUpConfirmedAsync(factory, Ct);
        var client = factory.CreateApiClient(user: null);
        return (client, email, await SignInAsync(client, email, password, Ct));
    }

    private async Task<T?> Owner<T>(string sql, string email)
    {
        await using var connection = new NpgsqlConnection(factory.OwnerConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email);
        var value = await command.ExecuteScalarAsync(Ct);
        return value is DBNull or null ? default : (T)value;
    }

    [Fact]
    public async Task Refresh_rotates_and_the_old_refresh_token_is_dead()
    {
        var (client, email, first) = await SignedIn();
        using var _ = client;

        var rotated = await Refresh(client, first.RefreshToken, Ct);
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        rotated.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var second = (await rotated.Content.ReadFromJsonAsync<AccessTokenResponse>(Ct))!;

        var reused = await Refresh(client, first.RefreshToken, Ct);
        var afterReuse = await Refresh(client, second.RefreshToken, Ct);

        reused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        afterReuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // Reuse and a revoked session look the same on the wire; only the log tells them apart.
        (await ProblemBody(afterReuse, Ct)).ShouldBe(await ProblemBody(reused, Ct));
        (await Owner<DateTime?>(
            "select s.revoked_at from accounts.sessions s join accounts.users u on u.id = s.user_id where u.email = @email", email)).ShouldNotBeNull();
        factory.Logs.Messages.ShouldContain(message => message.StartsWith("Refresh token reused", StringComparison.Ordinal));
        factory.Logs.Messages.ShouldContain("Refresh refused: accounts.session_revoked");
        factory.Logs.Messages.ShouldAllBe(message => !message.Contains(first.RefreshToken, StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_access_token_expires_with_no_skew()
    {
        var (client, _, tokens) = await SignedIn();
        using var _ = client;
        (await GetLists(client, tokens.AccessToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        factory.Clock.Advance(Options.AccessTokenLifetime + TimeSpan.FromSeconds(1));

        (await GetLists(client, tokens.AccessToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_refresh_token_one_second_past_its_lifetime_is_401()
    {
        var (client, _, tokens) = await SignedIn();
        using var _ = client;

        factory.Clock.Advance(Options.RefreshTokenLifetime + TimeSpan.FromSeconds(1));
        var response = await Refresh(client, tokens.RefreshToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        factory.Logs.Messages.ShouldContain("Refresh refused: expired");
    }

    [Fact]
    public async Task Refresh_after_the_user_is_deleted_is_401()
    {
        var (client, email, tokens) = await SignedIn();
        using var _ = client;

        await Owner<object>("delete from accounts.users where email = @email", email);

        (await Refresh(client, tokens.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Every_refusal_is_the_same_answer()
    {
        var (client, email, tokens) = await SignedIn();
        using var _ = client;
        var garbage = await Refresh(client, "not-a-token", Ct);
        await Owner<object>("delete from accounts.users where email = @email", email);
        var deleted = await Refresh(client, tokens.RefreshToken, Ct);

        garbage.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        deleted.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ProblemBody(deleted, Ct)).ShouldBe(await ProblemBody(garbage, Ct));
        garbage.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task Two_concurrent_refreshes_of_one_generation_let_one_win()
    {
        var (client, _, tokens) = await SignedIn();
        using var _ = client;

        var responses = await Task.WhenAll(Refresh(client, tokens.RefreshToken, Ct), Refresh(client, tokens.RefreshToken, Ct));

        // The loser meets the version token (409) or, if it read after the winner's commit, a stale generation (401).
        responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.ShouldContain(response => response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Conflict);
    }
}
