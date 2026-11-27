namespace Tsundoku.Engine;

/// <summary>A goal's plan: one to three areas, each a run of milestones.</summary>
public sealed record Curriculum(IReadOnlyList<Area> Areas);
