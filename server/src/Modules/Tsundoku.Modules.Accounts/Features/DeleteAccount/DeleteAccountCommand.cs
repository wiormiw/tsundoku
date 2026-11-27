using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.DeleteAccount;

internal sealed record DeleteAccountCommand(string? Password, string? GoogleIdToken) : ICommand<DeleteAccountOutcome>;
