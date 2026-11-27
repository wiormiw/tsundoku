namespace Tsundoku.Engine;

/// <summary>What <see cref="Fold.FoldGoal"/> folds.</summary>
/// <param name="Facts">The goal's facts, in any order.</param>
/// <param name="Today">The open day: earlier days are closed and classified, this one only reports.</param>
/// <param name="Rules">The tunables.</param>
/// <param name="Snapshot">Resume from the state after a closed day S; <paramref name="Facts"/> then start at S+1.</param>
/// <param name="OnDayClosed">
/// Called after each day this fold closes with the state a fold ending the next day gives (what a snapshot of that
/// day stores). A resume that closes nothing never calls it.
/// </param>
public sealed record FoldInput(
    IReadOnlyList<Fact> Facts,
    DateOnly Today,
    Rules Rules,
    GoalStateView? Snapshot = null,
    Action<GoalStateView>? OnDayClosed = null);
