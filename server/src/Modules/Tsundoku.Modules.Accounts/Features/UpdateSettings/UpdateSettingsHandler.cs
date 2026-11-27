using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.UpdateSettings;

internal sealed class UpdateSettingsHandler(AccountsDbContext db, ICurrentUser current) : ICommandHandler<UpdateSettingsCommand, SettingsResponse>
{
    public async Task<Result<SettingsResponse>> Handle(UpdateSettingsCommand command, CancellationToken cancellationToken) =>
        TimeZoneId.Create(command.Timezone) switch
        {
            TimeZoneId zone => await Upsert(zone, cancellationToken),
            Error error => error,
        };

    private async Task<Result<SettingsResponse>> Upsert(TimeZoneId zone, CancellationToken cancellationToken)
    {
        var owner = current.Id;
        if (await db.Settings.SingleOrDefaultAsync(s => s.Id == owner, cancellationToken) is { } settings) settings.ChangeTimeZone(zone);
        else db.Settings.Add(UserSettings.Create(owner, zone));
        return new SettingsResponse(zone.Value);
    }
}
