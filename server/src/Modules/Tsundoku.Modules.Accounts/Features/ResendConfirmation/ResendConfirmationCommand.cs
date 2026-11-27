using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.ResendConfirmation;

internal sealed record ResendConfirmationCommand(string Email) : ICommand<Success>;
