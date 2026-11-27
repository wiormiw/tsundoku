using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.SignIn;

/// <summary>The address and the user agent come from the endpoint, so the handler needs no HTTP context.</summary>
internal sealed record SignInCommand(string Email, string Password, string? IpAddress, string? UserAgent) : ICommand<SignInOutcome>;
