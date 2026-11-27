using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.GoogleSignIn;

/// <summary>The address and the user agent come from the endpoint, so the handler needs no HTTP context.</summary>
internal sealed record GoogleSignInCommand(string? IdToken, string? IpAddress, string? UserAgent) : ICommand<SignedIn>;
