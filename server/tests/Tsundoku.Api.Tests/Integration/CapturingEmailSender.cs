using System.Collections.Concurrent;
using Tsundoku.Modules.Accounts.Features;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>The mailer of the tests: keeps every mail in memory so a test can follow the link in it.</summary>
internal sealed class CapturingEmailSender : IAccountMailer
{
    internal sealed record Mail(string Email, string? Link);

    private readonly ConcurrentQueue<Mail> _mails = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _holds = new();

    /// <summary>Makes sends to <paramref name="email"/> wait until the returned handle is disposed, to show who is waiting for the mailer.</summary>
    public IDisposable Hold(string email)
    {
        var gate = _holds[email] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return new Release(gate);
    }

    private sealed class Release(TaskCompletionSource gate) : IDisposable
    {
        public void Dispose() => gate.TrySetResult();
    }

    private Task Wait(string email) => _holds.TryGetValue(email, out var gate) ? gate.Task : Task.CompletedTask;

    /// <summary>The mails sent to <paramref name="email"/>. A link is null for the "already registered" notice.</summary>
    public Mail[] To(string email) => [.. _mails.Where(mail => mail.Email == email)];

    public async Task SendConfirmationLinkAsync(AccountUser user, string email, string confirmationLink)
    {
        await Wait(email);
        _mails.Enqueue(new Mail(email, confirmationLink));
    }

    public async Task SendAlreadyRegisteredAsync(AccountUser user)
    {
        await Wait(user.Email!);
        _mails.Enqueue(new Mail(user.Email!, null));
    }

    public Task SendPasswordResetLinkAsync(AccountUser user, string email, string resetLink) => throw new NotSupportedException();

    public Task SendPasswordResetCodeAsync(AccountUser user, string email, string resetCode) => throw new NotSupportedException();
}
