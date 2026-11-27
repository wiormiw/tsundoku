using System.Text.Json.Serialization;

namespace Tsundoku.Engine;

/// <summary>
/// One entry of a goal's log: the envelope every fact shares, and one <c>Data</c> shape per <c>type</c>.
/// Closed, so a switch over the kinds of fact is exhaustive and a new kind is a compile error until the fold handles it.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GoalCreated), "goal.created")]
[JsonDerivedType(typeof(PlanApproved), "plan.approved")]
[JsonDerivedType(typeof(RevisionApproved), "revision.approved")]
[JsonDerivedType(typeof(ActivityLogged), "activity.logged")]
[JsonDerivedType(typeof(DayMarkedOffline), "day.marked_offline")]
[JsonDerivedType(typeof(DayUnmarkedOffline), "day.unmarked_offline")]
[JsonDerivedType(typeof(DebtRated), "debt.rated")]
[JsonDerivedType(typeof(RetroProlonged), "retro.prolonged")]
[JsonDerivedType(typeof(RetroEnded), "retro.ended")]
[JsonDerivedType(typeof(SettingsChanged), "settings.changed")]
public closed record Fact
{
    public required Guid Id { get; init; }
    public required Guid? GoalId { get; init; }

    /// <summary>A bigint; text on the wire, so no JS client rounds it.</summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required long Seq { get; init; }

    /// <summary>The envelope version, 1.</summary>
    public required int V { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }

    public sealed record GoalCreated(GoalCreated.Payload Data) : Fact
    {
        public sealed record Payload(string Title, IReadOnlyList<AreaName> Areas, int RangeActiveDays, DateOnly StartDate, string Timezone, Caps Caps);

        public sealed record AreaName(Guid Id, string Name);

        public sealed record Caps(int? OfflinePerMonth = null);
    }

    public sealed record PlanApproved(PlanApproved.Payload Data) : Fact
    {
        public sealed record Payload(int PlanVersion, Curriculum Curriculum);
    }

    public sealed record RevisionApproved(RevisionApproved.Payload Data) : Fact
    {
        public sealed record Payload(int PlanVersion, Curriculum Curriculum, RevisionReason Reason);
    }

    public sealed record ActivityLogged(ActivityLogged.Payload Data) : Fact
    {
        /// <summary>A double, as in the TS engine, so every sum and ratio rounds the same.</summary>
        public sealed record Payload(Guid ActivityId, double Amount, string? Note = null);
    }

    public sealed record DayMarkedOffline(DayPayload Data) : Fact;

    public sealed record DayUnmarkedOffline(DayPayload Data) : Fact;

    public sealed record DayPayload(DateOnly Date);

    public sealed record DebtRated(DebtRated.Payload Data) : Fact
    {
        public sealed record Payload(Guid AreaId, DateOnly Date, DebtRating Rating);
    }

    public sealed record RetroProlonged(RetroProlonged.Payload Data) : Fact
    {
        public sealed record Payload(int ExtraActiveDays);
    }

    public sealed record RetroEnded(RetroEnded.Payload Data) : Fact
    {
        public sealed record Payload;
    }

    public sealed record SettingsChanged(SettingsChanged.Payload Data) : Fact
    {
        public sealed record Payload(string? Timezone = null);
    }
}
