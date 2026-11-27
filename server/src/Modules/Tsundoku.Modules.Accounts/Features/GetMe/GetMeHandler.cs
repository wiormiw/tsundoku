using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.GetMe;

internal sealed class GetMeHandler(AccountsDbContext db, ICurrentUser current, IOptions<AccountsOptions> options) : IQueryHandler<GetMeQuery, MeResponse>
{
    public async Task<Result<MeResponse>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        var owner = current.Id;
        var id = Guid.Parse(owner.Value);
        var user = await db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new { u.Email, u.EmailConfirmed, HasPassword = u.PasswordHash != null })
            .SingleOrDefaultAsync(cancellationToken);
        // An access token outlives its user by up to its lifetime.
        if (user is null) return AccountErrors.SessionEnded;

        // No row yet means the default: the row is made on the first change.
        var settings = await db.Settings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == owner, cancellationToken);
        return new MeResponse(id, user.Email!, user.EmailConfirmed, user.HasPassword, settings?.TimeZone.Value ?? options.Value.DefaultTimeZone);
    }
}
