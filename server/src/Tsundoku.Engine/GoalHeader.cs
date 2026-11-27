namespace Tsundoku.Engine;

/// <summary>What a goal fixes before any plan: the areas a curriculum must cover and the range it must fill.</summary>
public sealed record GoalHeader(IReadOnlyList<Guid> AreaIds, int RangeActiveDays);
