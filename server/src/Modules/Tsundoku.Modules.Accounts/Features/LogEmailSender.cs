using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features;

/// <summary>
/// The development mailer. The link holds a confirmation code, so only Development logs it. Everywhere else the log says
/// that a mail was sent, with the user id and without the link or the address.
/// </summary>
internal sealed partial class LogEmailSender(IHostEnvironment environment, ILogger<LogEmailSender> logger) : IAccountMailer
{
    public Task SendConfirmationLinkAsync(AccountUser user, string email, string confirmationLink)
    {
        if (environment.IsDevelopment()) Log.ConfirmationLink(logger, user.Id, confirmationLink);
        else Log.Sent(logger, "confirmation", user.Id);
        return Task.CompletedTask;
    }

    public Task SendAlreadyRegisteredAsync(AccountUser user)
    {
        Log.Sent(logger, "already-registered", user.Id);
        return Task.CompletedTask;
    }

    // No route calls these: password reset is deferred.
    public Task SendPasswordResetLinkAsync(AccountUser user, string email, string resetLink) => throw new NotSupportedException();

    public Task SendPasswordResetCodeAsync(AccountUser user, string email, string resetCode) => throw new NotSupportedException();

    private static partial class Log
    {
        [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Confirmation mail for user {UserId}: {Link}")]
        public static partial void ConfirmationLink(ILogger logger, Guid userId, string link);

        [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "A {Kind} mail was sent to user {UserId}")]
        public static partial void Sent(ILogger logger, string kind, Guid userId);
    }
}
