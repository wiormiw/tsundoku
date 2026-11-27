using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.ConfirmEmail;

internal sealed record ConfirmEmailCommand(Guid UserId, string Code) : ICommand<Success>;
