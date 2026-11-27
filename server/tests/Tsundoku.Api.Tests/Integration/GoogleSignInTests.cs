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
}
