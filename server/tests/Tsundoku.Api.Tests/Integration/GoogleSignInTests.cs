using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsundoku.Modules.Accounts;
using static Tsundoku.Api.Tests.Integration.AccountsClient;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>POST /api/auth/google with ID tokens signed by the local test key, and deletion of a Google-only account.</summary>
public sealed class GoogleSignInTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Me(Guid Id, string Email, bool EmailConfirmed, bool HasPassword, string Timezone);

    private static string Mint(string subject, string email) => GoogleTokens.Mint(subject, email, DateTimeOffset.UtcNow);

    private static Task<HttpResponseMessage> Google(HttpClient client, string idToken) =>
        client.PostAsJsonAsync("/api/auth/google", new { idToken }, Ct);

    private static async Task<Me> MeOf(HttpClient client, AccessTokenResponse tokens)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var response = await client.SendAsync(request, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Me>(Ct))!;
    }

    private static async Task<AccessTokenResponse> GoogleSignedIn(HttpClient client, string subject, string email)
    {
        using var response = await Google(client, Mint(subject, email));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(Ct))!;
    }

    private async Task<long> UsersWithEmail(string email)
    {
        await using var connection = new NpgsqlConnection(factory.OwnerConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("select count(*) from accounts.users where email = @email", connection);
        command.Parameters.AddWithValue("email", email);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private async Task<long> GoogleLogins(Guid userId)
    {
        await using var connection = new NpgsqlConnection(factory.OwnerConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("select count(*) from accounts.user_logins where user_id = @id and login_provider = 'Google'", connection);
        command.Parameters.AddWithValue("id", userId);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    [Fact]
    public async Task A_new_google_user_is_created_confirmed_and_signed_in()
    {
        using var client = factory.CreateApiClient(user: null);
        var email = NewEmail();

        var tokens = await GoogleSignedIn(client, GoogleTokens.NewSubject(), email);

        var me = await MeOf(client, tokens);
        me.Email.ShouldBe(email);
        me.EmailConfirmed.ShouldBeTrue();
        me.HasPassword.ShouldBeFalse();
        tokens.RefreshToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task The_second_sign_in_reuses_the_login()
    {
        using var client = factory.CreateApiClient(user: null);
        var (subject, email) = (GoogleTokens.NewSubject(), NewEmail());

        var first = await MeOf(client, await GoogleSignedIn(client, subject, email));
        var second = await MeOf(client, await GoogleSignedIn(client, subject, email));

        second.Id.ShouldBe(first.Id);
        (await UsersWithEmail(email)).ShouldBe(1);
        (await GoogleLogins(first.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task A_confirmed_password_user_with_the_same_email_is_linked()
    {
        var (email, password) = await SignUpConfirmedAsync(factory, Ct);
        using var client = factory.CreateApiClient(user: null);
        var byPassword = await MeOf(client, await SignInAsync(client, email, password, Ct));

        var byGoogle = await MeOf(client, await GoogleSignedIn(client, GoogleTokens.NewSubject(), email));

        byGoogle.Id.ShouldBe(byPassword.Id);
        byGoogle.HasPassword.ShouldBeTrue();
        (await GoogleLogins(byPassword.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task An_unconfirmed_local_account_is_409_and_not_linked()
    {
        using var client = factory.CreateApiClient(user: null);
        var email = await SignUpAsync(client, Ct);

        using var response = await Google(client, Mint(GoogleTokens.NewSubject(), email));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.email_unconfirmed");
        await using var connection = new NpgsqlConnection(factory.OwnerConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "select count(*) from accounts.user_logins l join accounts.users u on u.id = l.user_id where u.email = @email", connection);
        command.Parameters.AddWithValue("email", email);
        ((long)(await command.ExecuteScalarAsync(Ct))!).ShouldBe(0);
    }

    [Fact]
    public async Task Closed_sign_up_creates_no_user_from_google()
    {
        using var closed = factory.WithWebHostBuilder(host => host.UseSetting("Accounts:SignUpEnabled", "false"));
        using var client = closed.CreateClient();
        var email = NewEmail();

        using var response = await Google(client, Mint(GoogleTokens.NewSubject(), email));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.sign_up_closed");
        (await UsersWithEmail(email)).ShouldBe(0);
    }

    [Fact]
    public async Task A_blank_client_id_means_Google_is_off()
    {
        Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
        using var blank = factory.WithWebHostBuilder(host => host.UseSetting("Accounts:Google:ClientIds:0", ""));
        using var client = blank.CreateClient();

        using var response = await Google(client, Mint(GoogleTokens.NewSubject(), NewEmail()));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.google_not_configured");
    }

    [Fact]
    public async Task Google_off_is_404()
    {
        Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
        using var off = factory.WithWebHostBuilder(host => host.ConfigureTestServices(services =>
            services.PostConfigure<GoogleOptions>(google => google.ClientIds = [])));
        using var client = off.CreateClient();

        using var response = await Google(client, Mint(GoogleTokens.NewSubject(), NewEmail()));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.google_not_configured");
    }

    [Fact]
    public async Task A_replayed_id_token_is_401()
    {
        using var client = factory.CreateApiClient(user: null);
        var token = Mint(GoogleTokens.NewSubject(), NewEmail());

        (await Google(client, token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var replayed = await Google(client, token);

        replayed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await replayed.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.invalid_credentials");
    }

    [Fact]
    public async Task A_google_only_user_deletes_with_a_fresh_id_token()
    {
        using var client = factory.CreateApiClient(user: null);
        var (subject, email) = (GoogleTokens.NewSubject(), NewEmail());
        var tokens = await GoogleSignedIn(client, subject, email);
        var id = (await MeOf(client, tokens)).Id;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        Task<HttpResponseMessage> Delete(string googleIdToken) => client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/me") { Content = JsonContent.Create(new { googleIdToken }) }, Ct);

        // Another Google account's valid token is no proof for this one.
        (await Delete(Mint(GoogleTokens.NewSubject(), NewEmail()))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Delete(Mint(subject, email))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await UsersWithEmail(email)).ShouldBe(0);
        (await GoogleLogins(id)).ShouldBe(0);
    }

    [Fact]
    public async Task A_second_google_account_with_the_same_email_is_not_linked()
    {
        using var client = factory.CreateApiClient(user: null);
        var email = NewEmail();
        var id = (await MeOf(client, await GoogleSignedIn(client, GoogleTokens.NewSubject(), email))).Id;

        // The address was recycled: a different Google account now owns it.
        using var response = await Google(client, Mint(GoogleTokens.NewSubject(), email));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.invalid_credentials");
        (await GoogleLogins(id)).ShouldBe(1);
    }

    [Fact]
    public async Task An_unverified_google_email_neither_signs_up_nor_links()
    {
        var (confirmed, _) = await SignUpConfirmedAsync(factory, Ct);
        using var client = factory.CreateApiClient(user: null);
        var fresh = NewEmail();

        foreach (var email in new[] { fresh, confirmed })
        {
            using var response = await Google(client, GoogleTokens.Mint(GoogleTokens.NewSubject(), email, DateTimeOffset.UtcNow, emailVerified: false));
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await UsersWithEmail(fresh)).ShouldBe(0);
        await using var connection = new NpgsqlConnection(factory.OwnerConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "select count(*) from accounts.user_logins l join accounts.users u on u.id = l.user_id where u.email = @email", connection);
        command.Parameters.AddWithValue("email", confirmed);
        ((long)(await command.ExecuteScalarAsync(Ct))!).ShouldBe(0);
    }

    [Fact]
    public async Task Parallel_first_sign_ins_of_one_google_account_create_one_user()
    {
        using var client = factory.CreateApiClient(user: null);
        var (subject, email) = (GoogleTokens.NewSubject(), NewEmail());

        // Without the per-sub lock, all but one would hit the unique user name and answer 409.
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Google(client, Mint(subject, email))));

        responses.Select(response => response.StatusCode).ShouldAllBe(status => status == HttpStatusCode.OK);
        (await UsersWithEmail(email)).ShouldBe(1);
        var id = (await MeOf(client, (await responses[0].Content.ReadFromJsonAsync<AccessTokenResponse>(Ct))!)).Id;
        (await GoogleLogins(id)).ShouldBe(1);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task A_password_sign_up_committing_first_makes_google_sign_in_409_not_500()
    {
        using var client = factory.CreateApiClient(user: null);
        var email = NewEmail();
        // A sign-up of the same address, inserted but not committed: Google's lookups miss it and its insert waits on the index.
        await using var signUp = new NpgsqlConnection(factory.OwnerConnectionString);
        await signUp.OpenAsync(Ct);
        await using var transaction = await signUp.BeginTransactionAsync(Ct);
        await using (var insert = new NpgsqlCommand("""
            insert into accounts.users (id, user_name, normalized_user_name, email, normalized_email, email_confirmed, security_stamp,
                phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            values (@id, @email, upper(@email), @email, upper(@email), false, 'stamp', false, false, true, 0)
            """, signUp, transaction))
        {
            insert.Parameters.AddWithValue("id", Guid.CreateVersion7());
            insert.Parameters.AddWithValue("email", email);
            await insert.ExecuteNonQueryAsync(Ct);
        }

        var google = Google(client, Mint(GoogleTokens.NewSubject(), email));
        await using (var watcher = new NpgsqlConnection(factory.OwnerConnectionString))
        {
            await watcher.OpenAsync(Ct);
            await using var waiting = new NpgsqlCommand(
                "select count(*) from pg_stat_activity where wait_event_type = 'Lock' and query ilike '%insert into accounts.users%'", watcher);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while ((long)(await waiting.ExecuteScalarAsync(Ct))! == 0)
            {
                if (DateTimeOffset.UtcNow > deadline) throw new TimeoutException("The Google sign-in never waited on the sign-up's insert.");
                await Task.Delay(20, Ct);
            }
        }
        await transaction.CommitAsync(Ct);

        using var response = await google;
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.sign_up_conflict");
        (await UsersWithEmail(email)).ShouldBe(1);
    }

    [Fact]
    public async Task Deleting_with_an_old_id_token_needs_a_fresh_sign_in()
    {
        using var client = factory.CreateApiClient(user: null);
        var (subject, email) = (GoogleTokens.NewSubject(), NewEmail());
        var tokens = await GoogleSignedIn(client, subject, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        // Still unexpired (an hour's lifetime), but issued longer ago than ReauthenticationMaxAge (5 min).
        var old = GoogleTokens.Mint(subject, email, DateTimeOffset.UtcNow, issuedOffset: TimeSpan.FromMinutes(-10), expiresOffset: TimeSpan.FromMinutes(50));
        using var refused = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/me") { Content = JsonContent.Create(new { googleIdToken = old }) }, Ct);

        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.reauthentication_required");
        (await UsersWithEmail(email)).ShouldBe(1);
    }
}
