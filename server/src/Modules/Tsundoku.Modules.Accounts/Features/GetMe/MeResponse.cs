namespace Tsundoku.Modules.Accounts.Features.GetMe;

internal sealed record MeResponse(Guid Id, string Email, bool EmailConfirmed, bool HasPassword, string Timezone);
