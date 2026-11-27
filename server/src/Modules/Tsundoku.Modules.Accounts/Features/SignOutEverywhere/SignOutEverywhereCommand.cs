using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.SignOutEverywhere;

internal sealed record SignOutEverywhereCommand : ICommand<Success>;
