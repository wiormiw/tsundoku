using Microsoft.Extensions.Options;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Features;

namespace Tsundoku.Modules.Accounts;

/// <summary>The rules that data annotations cannot say. The domain decides what a time zone is, so the startup check asks it.</summary>
internal sealed class AccountsOptionsValidator(IHostEnvironment environment, IAccountMailer mailer) : IValidateOptions<AccountsOptions>
{
    public ValidateOptionsResult Validate(string? name, AccountsOptions options)
    {
        List<string> failures = [];
        if (options.PublicBaseUrl is not null && !options.PublicBaseUrl.IsAbsoluteUri)
            failures.Add($"{AccountsOptions.Section}:{nameof(AccountsOptions.PublicBaseUrl)} must be an absolute URL.");
        if (TimeZoneId.Create(options.DefaultTimeZone) is Error)
            failures.Add($"{AccountsOptions.Section}:{nameof(AccountsOptions.DefaultTimeZone)} '{options.DefaultTimeZone}' is not an IANA time zone id.");
        // The log mailer cannot deliver, so a real user could never confirm; and it must not write codes to production logs.
        if (options.SignUpEnabled && !environment.IsDevelopment() && mailer is LogEmailSender)
            failures.Add($"{AccountsOptions.Section}:{nameof(AccountsOptions.SignUpEnabled)} needs a real mailer (B9); use Google sign-in until then.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
