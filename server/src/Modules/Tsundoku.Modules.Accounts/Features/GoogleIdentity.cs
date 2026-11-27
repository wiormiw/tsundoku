namespace Tsundoku.Modules.Accounts.Features;

/// <summary>The claims of a verified Google ID token that sign-in needs.</summary>
internal sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, DateTimeOffset IssuedAt);
