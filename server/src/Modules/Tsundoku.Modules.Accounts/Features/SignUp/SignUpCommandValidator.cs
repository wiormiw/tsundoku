using FluentValidation;
using Microsoft.Extensions.Options;

namespace Tsundoku.Modules.Accounts.Features.SignUp;

internal sealed class SignUpCommandValidator : AbstractValidator<SignUpCommand>
{
    public SignUpCommandValidator(IOptions<AccountsOptions> options)
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Password).NotEmpty().MinimumLength(options.Value.PasswordMinLength).MaximumLength(PasswordRules.MaxLength);
    }
}
