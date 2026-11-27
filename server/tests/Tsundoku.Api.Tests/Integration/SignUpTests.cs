using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Tsundoku.Modules.Accounts.Features;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Api.Tests.Integration;

public sealed class SignUpTests(ApiFactory factory)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.test";

    private static Task<HttpResponseMessage> SignUp(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/sign-up", new { email, password = Password }, Ct);

    private static Task<HttpResponseMessage> Resend(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email }, Ct);

    private static string LinkOf(CapturingEmailSender mail, string email) => mail.To(email).Single(m => m.Link is not null).Link!;

    private async Task<bool?> EmailConfirmed(string email)
    {
        await using var connection = new NpgsqlConnection(factory.OwnerConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("select email_confirmed from accounts.users where email = @email", connection);
        command.Parameters.AddWithValue("email", email);
        return await command.ExecuteScalarAsync(Ct) as bool?;
    }

    [Fact]
    public async Task Sign_up_then_confirm_marks_the_email_confirmed()
    {
        using var client = factory.CreateApiClient(user: null);
        var email = NewEmail();

        (await SignUp(client, email)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await EmailConfirmed(email)).ShouldBe(false);

        var confirm = await client.GetAsync(new Uri(LinkOf(factory.Mail, email)).PathAndQuery, Ct);

        confirm.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await confirm.Content.ReadAsStringAsync(Ct)).ShouldBe("Your email is confirmed. You can sign in now.");
        (await EmailConfirmed(email)).ShouldBe(true);
    }

    [Fact]
    public async Task New_and_existing_emails_get_identical_answers()
    {
        var counter = new HashCounter();
        using var counting = factory.WithWebHostBuilder(host => host.ConfigureServices(services =>
        {
            services.AddSingleton(counter);
            services.RemoveAll<IPasswordHasher<AccountUser>>();
            services.AddScoped<IPasswordHasher<AccountUser>, CountingHasher>();
        }));
        using var client = counting.CreateClient();
        var email = NewEmail();

        var first = await SignUp(client, email);
        var firstCalls = counter.Take();
        var again = await SignUp(client, email);
        var againCalls = counter.Take();
        var other = await SignUp(client, NewEmail());
        var otherCalls = counter.Take();

        foreach (var response in new[] { first, again, other })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await response.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
            HeaderNames(response).ShouldBe(HeaderNames(first));
        }

        // One hash on each branch, so the time does not give the address away (Review Focus 4).
        (firstCalls, againCalls, otherCalls).ShouldBe((1, 1, 1));
        factory.Mail.To(email).Count(mail => mail.Link is null).ShouldBe(1);

        static string[] HeaderNames(HttpResponseMessage response) =>
            [.. response.Headers.Concat(response.Content.Headers).Select(h => h.Key).Where(k => k != "Date").Order()];
    }

    [Fact]
    public async Task Closed_sign_up_is_403_for_new_and_existing_emails()
    {
        var existing = NewEmail();
        using (var open = factory.CreateApiClient(user: null)) (await SignUp(open, existing)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using var closed = factory.WithWebHostBuilder(host => host.UseSetting("Accounts:SignUpEnabled", "false"));
        using var client = closed.CreateClient();
        var fresh = NewEmail();

        foreach (var email in new[] { existing, fresh })
        {
            var response = await SignUp(client, email);
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.sign_up_closed");
        }

        (await EmailConfirmed(fresh)).ShouldBeNull();
    }

    [Fact]
    public async Task A_tampered_code_or_unknown_user_is_400()
    {
        using var client = factory.CreateApiClient(user: null);
        var email = NewEmail();
        await SignUp(client, email);
        var link = LinkOf(factory.Mail, email);
        var tampered = link[..^1] + (link[^1] == 'A' ? 'B' : 'A');
        var unknownUser = link.Replace("userId=", $"userId={Guid.NewGuid()}&was=", StringComparison.Ordinal);

        foreach (var bad in new[] { tampered, unknownUser, link[..link.IndexOf("&code=", StringComparison.Ordinal)] + "&code=!!!" })
        {
            var response = await client.GetAsync(new Uri(bad).PathAndQuery, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.confirmation_invalid");
        }

        (await EmailConfirmed(email)).ShouldBe(false);
    }

    [Fact]
    public async Task Resend_is_202_for_unknown_confirmed_and_unconfirmed_alike()
    {
        using var client = factory.CreateApiClient(user: null);
        var unconfirmed = NewEmail();
        var confirmed = NewEmail();
        await SignUp(client, unconfirmed);
        await SignUp(client, confirmed);
        await client.GetAsync(new Uri(LinkOf(factory.Mail, confirmed)).PathAndQuery, Ct);
        var unknown = NewEmail();

        foreach (var email in new[] { unknown, confirmed, unconfirmed })
        {
            var response = await Resend(client, email);
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await response.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
        }

        factory.Mail.To(unknown).ShouldBeEmpty();
        factory.Mail.To(confirmed).Length.ShouldBe(1);
        factory.Mail.To(unconfirmed).Length.ShouldBe(2);
    }

    [Fact]
    public async Task Nothing_secret_is_logged()
    {
        var logs = new CapturingLoggerProvider();
        // The real log mailer, so its lines are in the capture too: they carry the user id and the link, never the address.
        using var logging = factory.WithWebHostBuilder(host => host
            .ConfigureServices(services =>
            {
                services.AddLogging(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
                services.RemoveAll<IAccountMailer>();
                services.RemoveAll<IEmailSender<AccountUser>>();
                services.AddSingleton<IAccountMailer, LogEmailSender>();
                services.AddSingleton<IEmailSender<AccountUser>>(provider => provider.GetRequiredService<IAccountMailer>());
            }));
        using var client = logging.CreateClient();
        var email = NewEmail();

        await SignUp(client, email);
        await SignUp(client, email);
        await Resend(client, email);

        logs.Messages.ShouldNotBeEmpty();
        logs.Messages.ShouldAllBe(message => !message.Contains(Password, StringComparison.Ordinal) && !message.Contains(email, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class HashCounter
    {
        private int _calls;

        public void Add() => Interlocked.Increment(ref _calls);

        public int Take() => Interlocked.Exchange(ref _calls, 0);
    }

    private sealed class CountingHasher(IOptions<PasswordHasherOptions> options, HashCounter counter) : IPasswordHasher<AccountUser>
    {
        private readonly PasswordHasher<AccountUser> _inner = new(options);

        public string HashPassword(AccountUser user, string password)
        {
            counter.Add();
            return _inner.HashPassword(user, password);
        }

        public PasswordVerificationResult VerifyHashedPassword(AccountUser user, string hashedPassword, string providedPassword)
        {
            counter.Add();
            return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}
