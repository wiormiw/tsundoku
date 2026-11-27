using Microsoft.Extensions.Options;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Accounts.Domain;

namespace Tsundoku.Modules.Accounts;

/// <summary>The rules that data annotations cannot say. The domain decides what a time zone is, so the startup check asks it.</summary>
internal sealed class AccountsOptionsValidator : IValidateOptions<AccountsOptions>
{
    public ValidateOptionsResult Validate(string? name, AccountsOptions options)
    {
        List<string> failures = [];
        if (options.PublicBaseUrl is not null && !options.PublicBaseUrl.IsAbsoluteUri)
            failures.Add($"{AccountsOptions.Section}:{nameof(AccountsOptions.PublicBaseUrl)} must be an absolute URL.");
        if (TimeZoneId.Create(options.DefaultTimeZone) is Error)
            failures.Add($"{AccountsOptions.Section}:{nameof(AccountsOptions.DefaultTimeZone)} '{options.DefaultTimeZone}' is not an IANA time zone id.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
