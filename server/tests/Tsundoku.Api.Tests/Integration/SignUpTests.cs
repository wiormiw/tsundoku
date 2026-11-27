using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
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

    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline) throw new TimeoutException("The outbox did not send the mail.");
            await Task.Delay(50, Ct);
        }
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.test";

    private static Task<HttpResponseMessage> SignUp(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/sign-up", new { email, password = Password }, Ct);

    private static Task<HttpResponseMessage> Resend(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email }, Ct);

    /// <summary>Mails go out after the commit through the outbox, so wait for the <paramref name="count"/>th link to arrive.</summary>
    private static async Task<string> LinkOf(CapturingEmailSender mail, string email, int count = 1)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (mail.To(email).Count(m => m.Link is not null) < count)
        {
            if (DateTimeOffset.UtcNow > deadline) throw new TimeoutException("The outbox did not send the confirmation mail.");
            await Task.Delay(50, Ct);
        }
        return mail.To(email).Where(m => m.Link is not null).ElementAt(count - 1).Link!;
    }

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

        var confirm = await client.GetAsync(new Uri(await LinkOf(factory.Mail, email)).PathAndQuery, Ct);

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
        await WaitFor(() => factory.Mail.To(email).Count(mail => mail.Link is null) == 1);

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
        var link = await LinkOf(factory.Mail, email);
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
        await client.GetAsync(new Uri(await LinkOf(factory.Mail, confirmed)).PathAndQuery, Ct);
        var unknown = NewEmail();

        foreach (var email in new[] { unknown, confirmed, unconfirmed })
        {
            var response = await Resend(client, email);
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await response.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
        }

        await LinkOf(factory.Mail, unconfirmed, 2);
        factory.Mail.To(unknown).ShouldBeEmpty();
        factory.Mail.To(confirmed).Length.ShouldBe(1);
        factory.Mail.To(unconfirmed).Length.ShouldBe(2);
    }

    [Fact]
    public async Task Nothing_secret_is_logged()
    {
        var logs = new CapturingLoggerProvider();
        // The real log mailer in Development, where it may log the link: the capture must still hold no address and no password.
        using var logging = factory.WithWebHostBuilder(host => host
            .UseEnvironment("Development")
            .ConfigureServices(services =>
            {
                services.AddLogging(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
                services.RemoveAll<IAccountMailer>();
                services.RemoveAll<IEmailSender<AccountUser>>();
                // Also into the shared capture: this host's outbox may claim another test's mail, which must still arrive there.
                services.AddSingleton<IAccountMailer>(provider => new LoggedAndCaptured(provider.GetRequiredService<LogEmailSender>(), factory.Mail));
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

    [Fact]
    public async Task The_mail_goes_out_after_the_commit_through_the_outbox_not_in_the_request()
    {
        using var client = factory.CreateApiClient(user: null);
        var email = NewEmail();

        // The mailer waits here. A request that called it would hang, so the 202 shows it does not.
        using (factory.Mail.Hold(email))
        {
            (await SignUp(client, email)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
            factory.Mail.To(email).ShouldBeEmpty();

            await using var connection = new NpgsqlConnection(factory.OwnerConnectionString);
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand(
                "select type, payload::text from accounts.outbox_messages where payload::text like '%' || (select id::text from accounts.users where email = @email) || '%'",
                connection);
            command.Parameters.AddWithValue("email", email);
            await using var reader = await command.ExecuteReaderAsync(Ct);
            (await reader.ReadAsync(Ct)).ShouldBeTrue();
            reader.GetString(0).ShouldBe("ConfirmationRequested");
            // Only the user id: no token and no link in the outbox.
            System.Text.Json.JsonElement.Parse(reader.GetString(1)).EnumerateObject().Count().ShouldBe(1);
        }

        (await LinkOf(factory.Mail, email)).ShouldContain("code=");
    }

    [Fact]
    public async Task A_used_confirmation_link_is_400_the_second_time()
    {
        using var client = factory.CreateApiClient(user: null);
        var email = NewEmail();
        await SignUp(client, email);
        var link = new Uri(await LinkOf(factory.Mail, email)).PathAndQuery;

        (await client.GetAsync(link, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var again = await client.GetAsync(link, Ct);

        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await again.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.confirmation_invalid");
    }

    [Fact]
    public async Task An_expired_confirmation_link_is_400()
    {
        // Identity stamps and checks token times with the system clock, not TimeProvider, so a short lifespan stands in for a clock change.
        using var shortLived = factory.WithWebHostBuilder(host => host.ConfigureTestServices(services =>
            services.Configure<DataProtectionTokenProviderOptions>(token => token.TokenLifespan = TimeSpan.FromSeconds(1))));
        using var client = shortLived.CreateClient();
        var email = NewEmail();
        await SignUp(client, email);
        var link = new Uri(await LinkOf(factory.Mail, email)).PathAndQuery;
        await Task.Delay(TimeSpan.FromSeconds(2), Ct);

        var response = await client.GetAsync(link, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("accounts.confirmation_invalid");
        (await EmailConfirmed(email)).ShouldBe(false);
    }

    [Fact]
    public void Startup_refuses_open_sign_up_with_only_the_log_mailer_outside_Development()
    {
        Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
        using var logOnly = factory.WithWebHostBuilder(host => host.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountMailer>();
            services.RemoveAll<IEmailSender<AccountUser>>();
            services.AddSingleton<IAccountMailer, LogEmailSender>();
        }));

        var failure = Should.Throw<OptionsValidationException>(() => logOnly.CreateClient());

        failure.Message.ShouldContain("needs a real mailer (B9); use Google sign-in until then");
    }

    private sealed class LoggedAndCaptured(LogEmailSender log, CapturingEmailSender capture) : IAccountMailer
    {
        public async Task SendConfirmationLinkAsync(AccountUser user, string email, string confirmationLink)
        {
            await log.SendConfirmationLinkAsync(user, email, confirmationLink);
            await capture.SendConfirmationLinkAsync(user, email, confirmationLink);
        }

        public async Task SendAlreadyRegisteredAsync(AccountUser user)
        {
            await log.SendAlreadyRegisteredAsync(user);
            await capture.SendAlreadyRegisteredAsync(user);
        }

        public Task SendPasswordResetLinkAsync(AccountUser user, string email, string resetLink) => throw new NotSupportedException();

        public Task SendPasswordResetCodeAsync(AccountUser user, string email, string resetCode) => throw new NotSupportedException();
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
