namespace Tsundoku.Modules.Accounts.Features;

/// <summary>The password bound that sign-up and sign-in share.</summary>
internal static class PasswordRules
{
    /// <summary>The upper bound stops a huge body from reaching the hasher.</summary>
    public const int MaxLength = 128;
}
