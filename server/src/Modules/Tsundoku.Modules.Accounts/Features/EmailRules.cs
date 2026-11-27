using FluentValidation;

namespace Tsundoku.Modules.Accounts.Features;

/// <summary>The email rule that sign-up and resend share.</summary>
internal static class EmailRules
{
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(256).EmailAddress();
}
