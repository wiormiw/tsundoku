using System.Collections.Concurrent;
using Tsundoku.Modules.Accounts.Features;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>The mailer of the tests: keeps every mail in memory so a test can follow the link in it.</summary>
internal sealed class CapturingEmailSender : IAccountMailer
{
    internal sealed record Mail(string Email, string? Link);

    private readonly ConcurrentQueue<Mail> _mails = new();

    /// <summary>The mails sent to <paramref name="email"/>. A link is null for the "already registered" notice.</summary>
    public Mail[] To(string email) => [.. _mails.Where(mail => mail.Email == email)];

    public Task SendConfirmationLinkAsync(AccountUser user, string email, string confirmationLink)
    {
        _mails.Enqueue(new Mail(email, confirmationLink));
        return Task.CompletedTask;
    }

    public Task SendAlreadyRegisteredAsync(AccountUser user)
    {
        _mails.Enqueue(new Mail(user.Email!, null));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(AccountUser user, string email, string resetLink) => throw new NotSupportedException();

    public Task SendPasswordResetCodeAsync(AccountUser user, string email, string resetCode) => throw new NotSupportedException();
}
