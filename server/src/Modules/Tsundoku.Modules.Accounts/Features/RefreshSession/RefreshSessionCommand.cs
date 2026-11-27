using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.RefreshSession;

internal sealed record RefreshSessionCommand(string RefreshToken) : ICommand<RefreshOutcome>;
