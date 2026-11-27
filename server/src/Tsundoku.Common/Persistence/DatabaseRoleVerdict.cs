namespace Tsundoku.Common.Persistence;

/// <summary>What the readiness check remembers for the process: the role is checked until it is seen safe, and an unsafe one is logged once.</summary>
internal sealed class DatabaseRoleVerdict
{
    private int _safe;
    private int _logged;

    public bool IsSafe => Volatile.Read(ref _safe) == 1;

    public void MarkSafe() => Volatile.Write(ref _safe, 1);

    /// <summary>True for the first caller only.</summary>
    public bool ClaimLog() => Interlocked.Exchange(ref _logged, 1) == 0;
}
