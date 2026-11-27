using FluentValidation;

namespace Tsundoku.Modules.Accounts.Features.ResendConfirmation;

internal sealed class ResendConfirmationCommandValidator : AbstractValidator<ResendConfirmationCommand>
{
    public ResendConfirmationCommandValidator() => RuleFor(command => command.Email).ValidEmail();
}
