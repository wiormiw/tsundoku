namespace Tsundoku.Engine;

/// <summary>A run of <paramref name="Days"/> active days with its activities.</summary>
public sealed record Milestone(Guid Id, string Title, int Days, IReadOnlyList<Activity> Activities);
