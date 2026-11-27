using FluentValidation;
using Microsoft.Extensions.Options;

namespace Tsundoku.Modules.Accounts.Features.SignUp;

internal sealed class SignUpCommandValidator : AbstractValidator<SignUpCommand>
{
    /// <summary>The upper bound stops a huge body from reaching the hasher.</summary>
    internal const int PasswordMaxLength = 128;

    public SignUpCommandValidator(IOptions<AccountsOptions> options)
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Password).NotEmpty().MinimumLength(options.Value.PasswordMinLength).MaximumLength(PasswordMaxLength);
    }
}
