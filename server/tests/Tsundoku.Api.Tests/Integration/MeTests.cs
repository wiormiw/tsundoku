using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Npgsql;
using static Tsundoku.Api.Tests.Integration.AccountsClient;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>/me, settings, sign-out everywhere and deletion, through real tokens.</summary>
public sealed class MeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Client, string Email, AccessTokenResponse Tokens)> SignedIn()
    {
        var (email, password) = await SignUpConfirmedAsync(factory, Ct);
        var client = factory.CreateApiClient(user: null);
        var tokens = await SignInAsync(client, email, password, Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (client, email, tokens);
    }

    private async Task<long> OwnerCount(string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(factory.OwnerConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private static Task<HttpResponseMessage> Delete(HttpClient client, object? body) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me") { Content = body is null ? null : JsonContent.Create(body) }, Ct);

    private static async Task<string> Timezone(HttpClient client) =>
        (await (await client.GetAsync("/api/me", Ct)).Content.ReadFromJsonAsync<Me>(Ct))!.Timezone;

    private sealed record Me(Guid Id, string Email, bool EmailConfirmed, bool HasPassword, string Timezone);

    [Fact]
    public async Task Me_returns_the_default_time_zone_before_any_setting()
    {
        var (client, email, _) = await SignedIn();
        using var _ = client;

        var me = (await (await client.GetAsync("/api/me", Ct)).Content.ReadFromJsonAsync<Me>(Ct))!;

        me.Timezone.ShouldBe("Asia/Jakarta");
        me.HasPassword.ShouldBeTrue();
        me.EmailConfirmed.ShouldBeTrue();
        me.Email.ShouldBe(email);
    }

    [Fact]
    public async Task Patch_sets_Asia_Tokyo_and_me_reads_it()
    {
        var (client, _, _) = await SignedIn();
        using var _ = client;

        var patched = await client.PatchAsJsonAsync("/api/me/settings", new { timezone = "Asia/Tokyo" }, Ct);
        patched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await patched.Content.ReadAsStringAsync(Ct)).ShouldContain("Asia/Tokyo");
        (await Timezone(client)).ShouldBe("Asia/Tokyo");

        // A second change updates the row.
        (await client.PatchAsJsonAsync("/api/me/settings", new { timezone = "Europe/Berlin" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Timezone(client)).ShouldBe("Europe/Berlin");
    }

    [Theory]
    [InlineData("Mars/Base")]
    [InlineData("SE Asia Standard Time")]
    public async Task An_unknown_or_windows_time_zone_is_400_on_field_timezone(string zone)
    {
        var (client, _, _) = await SignedIn();
        using var _ = client;

        var response = await client.PatchAsJsonAsync("/api/me/settings", new { timezone = zone }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"timezone\"");
    }

    [Fact]
    public async Task Me_without_a_token_is_401()
    {
        using var client = factory.CreateApiClient(user: null);

        (await client.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task B_never_sees_or_changes_A_settings()
    {
        var (a, _, _) = await SignedIn();
        var (b, _, _) = await SignedIn();
        using var _ = a;
        using var __ = b;

        (await a.PatchAsJsonAsync("/api/me/settings", new { timezone = "Asia/Tokyo" }, Ct)).EnsureSuccessStatusCode();
        (await b.PatchAsJsonAsync("/api/me/settings", new { timezone = "Europe/Berlin" }, Ct)).EnsureSuccessStatusCode();

        (await Timezone(a)).ShouldBe("Asia/Tokyo");
        (await Timezone(b)).ShouldBe("Europe/Berlin");
        var idA = (await (await a.GetAsync("/api/me", Ct)).Content.ReadFromJsonAsync<Me>(Ct))!.Id;
        var idB = (await (await b.GetAsync("/api/me", Ct)).Content.ReadFromJsonAsync<Me>(Ct))!.Id;
        // As the owner: one row each, each under its own user_id.
        (await OwnerCount("select count(*) from accounts.user_settings where user_id = @id", idA)).ShouldBe(1);
        (await OwnerCount("select count(*) from accounts.user_settings where user_id = @id", idB)).ShouldBe(1);
    }

    [Fact]
    public async Task Sign_out_everywhere_kills_every_refresh_token()
    {
        var (client, email, first) = await SignedIn();
        using var _ = client;
        using var other = factory.CreateApiClient(user: null);
        var second = await SignInAsync(other, email, Password, Ct);

        (await client.DeleteAsync("/api/me/sessions", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Refresh(other, first.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(other, second.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // The user can still sign in again.
        (await SignIn(other, email, Password, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_without_a_password_is_400_and_with_a_wrong_one_401()
    {
        var (client, _, _) = await SignedIn();
        using var _ = client;

        var missing = await Delete(client, null);
        var empty = await Delete(client, new { });
        var wrong = await Delete(client, new { password = "not the password at all" });

        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await missing.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.reauthentication_required");
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_with_the_password_leaves_no_rows()
    {
        var (client, _, _) = await SignedIn();
        using var _ = client;
        (await client.PatchAsJsonAsync("/api/me/settings", new { timezone = "Asia/Tokyo" }, Ct)).EnsureSuccessStatusCode();
        var id = (await (await client.GetAsync("/api/me", Ct)).Content.ReadFromJsonAsync<Me>(Ct))!.Id;

        (await Delete(client, new { password = Password })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await OwnerCount("select count(*) from accounts.users where id = @id", id)).ShouldBe(0);
        (await OwnerCount("select count(*) from accounts.sessions where user_id = @id", id)).ShouldBe(0);
        (await OwnerCount("select count(*) from accounts.user_settings where user_id = @id", id)).ShouldBe(0);
        // The access token is still valid, but its user is gone.
        var after = await client.GetAsync("/api/me", Ct);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await after.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.session_ended");
    }

    [Fact]
    public async Task A_deleted_users_refresh_token_is_401()
    {
        var (client, _, tokens) = await SignedIn();
        using var _ = client;

        (await Delete(client, new { password = Password })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Refresh(client, tokens.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_passwords_on_delete_count_towards_lockout()
    {
        var (client, email, _) = await SignedIn();
        using var _ = client;

        for (var i = 0; i < 10; i++) await Delete(client, new { password = "not the password at all" });

        // Locked out: even the right password is refused now, on delete and on sign-in.
        (await Delete(client, new { password = Password })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await SignIn(client, email, Password, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Settings_of_a_deleted_user_with_the_old_access_token_are_401_session_ended()
    {
        var (client, _, _) = await SignedIn();
        using var _ = client;
        (await Delete(client, new { password = Password })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await client.PatchAsJsonAsync("/api/me/settings", new { timezone = "Asia/Tokyo" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.session_ended");
    }

    [Fact]
    public async Task Parallel_first_patches_all_succeed_and_leave_one_row()
    {
        var (client, _, _) = await SignedIn();
        using var _ = client;
        var zones = new[] { "Asia/Tokyo", "Europe/Berlin", "Asia/Seoul", "America/Lima", "Africa/Cairo", "Asia/Jakarta" };

        var responses = await Task.WhenAll(zones.Select(zone => client.PatchAsJsonAsync("/api/me/settings", new { timezone = zone }, Ct)));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK);
        var id = (await (await client.GetAsync("/api/me", Ct)).Content.ReadFromJsonAsync<Me>(Ct))!.Id;
        (await OwnerCount("select count(*) from accounts.user_settings where user_id = @id", id)).ShouldBe(1);
        zones.ShouldContain(await Timezone(client));
    }
}
