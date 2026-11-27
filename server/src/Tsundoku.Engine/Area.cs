namespace Tsundoku.Engine;

public sealed record Area(Guid Id, string Name, IReadOnlyList<Milestone> Milestones);
