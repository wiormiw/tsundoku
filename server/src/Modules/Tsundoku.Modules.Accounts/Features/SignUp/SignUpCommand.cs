using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.SignUp;

internal sealed record SignUpCommand(string Email, string Password) : ICommand<Success>;
