namespace Tsundoku.Modules.Accounts.Features.DeleteAccount;

internal sealed record DeleteAccountRequest(string? Password, string? GoogleIdToken);
