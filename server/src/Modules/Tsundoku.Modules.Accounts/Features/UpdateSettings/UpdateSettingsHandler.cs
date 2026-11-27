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
        // One write per user at a time, until the commit: of two first writes the second sees the row and updates it,
        // instead of both inserting and one failing on the key. It runs in the decorator's transaction, so RLS still applies.
        var lockKey = $"accounts.settings:{owner.Value}";
        await db.Database.ExecuteSqlAsync($"select pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);

        // An access token outlives its user by up to its lifetime, and the settings row has a foreign key to the user.
        var id = Guid.Parse(owner.Value);
        if (!await db.Users.AnyAsync(u => u.Id == id, cancellationToken)) return AccountErrors.SessionEnded;

        if (await db.Settings.SingleOrDefaultAsync(s => s.Id == owner, cancellationToken) is { } settings) settings.ChangeTimeZone(zone);
        else db.Settings.Add(UserSettings.Create(owner, zone));
        return new SettingsResponse(zone.Value);
    }
}
