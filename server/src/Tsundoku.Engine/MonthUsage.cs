namespace Tsundoku.Engine;

/// <summary>Offline plus skipped days used in a month, keyed <c>YYYY-MM</c>.</summary>
public sealed record MonthUsage(string Month, int Used);
