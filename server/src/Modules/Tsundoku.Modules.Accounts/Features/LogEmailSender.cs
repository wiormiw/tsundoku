using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features;

/// <summary>The development mailer: it writes the mail to the log, with the user id and never the address.</summary>
internal sealed partial class LogEmailSender(ILogger<LogEmailSender> logger) : IAccountMailer
{
    public Task SendConfirmationLinkAsync(AccountUser user, string email, string confirmationLink)
    {
        Log.ConfirmationLink(logger, user.Id, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendAlreadyRegisteredAsync(AccountUser user)
    {
        Log.AlreadyRegistered(logger, user.Id);
        return Task.CompletedTask;
    }

    // No route calls these: password reset is deferred.
    public Task SendPasswordResetLinkAsync(AccountUser user, string email, string resetLink) => throw new NotSupportedException();

    public Task SendPasswordResetCodeAsync(AccountUser user, string email, string resetCode) => throw new NotSupportedException();

    private static partial class Log
    {
        [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Confirmation mail for user {UserId}: {Link}")]
        public static partial void ConfirmationLink(ILogger logger, Guid userId, string link);

        [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Already-registered mail for user {UserId}: someone tried to sign up with this address; sign in instead.")]
        public static partial void AlreadyRegistered(ILogger logger, Guid userId);
    }
}
