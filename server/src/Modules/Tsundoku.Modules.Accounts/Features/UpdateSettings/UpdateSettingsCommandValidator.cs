using FluentValidation;
using Tsundoku.Modules.Accounts.Domain;

namespace Tsundoku.Modules.Accounts.Features.UpdateSettings;

internal sealed class UpdateSettingsCommandValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsCommandValidator() =>
        RuleFor(command => command.Timezone)
            .Must(zone => TimeZoneId.Create(zone) is TimeZoneId)
            .WithMessage("Must be an IANA time zone id such as Asia/Jakarta.");
}
