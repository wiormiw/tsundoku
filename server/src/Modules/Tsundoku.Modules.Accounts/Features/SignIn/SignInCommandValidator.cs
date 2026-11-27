using FluentValidation;

namespace Tsundoku.Modules.Accounts.Features.SignIn;

/// <summary>Shape only: a weak password is still checked against the hash, so the rules of sign-up do not apply here.</summary>
internal sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}
