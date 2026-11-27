namespace Tsundoku.Modules.Accounts.Domain;

/// <summary>The id of a sign-in session. Version 7 GUIDs sort by creation time.</summary>
internal readonly record struct AccountSessionId(Guid Value)
{
    public static AccountSessionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
