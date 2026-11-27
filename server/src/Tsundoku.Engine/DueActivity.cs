namespace Tsundoku.Engine;

/// <summary>An activity due on a day, with the area and milestone it belongs to.</summary>
public sealed record DueActivity(Guid AreaId, Activity Activity, Guid MilestoneId);
