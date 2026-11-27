using System.Globalization;

namespace Tsundoku.Engine.Tests;

public sealed class ScheduleTests
{
    private static Guid Id(int n) => Guid.Parse("00000000-0000-4000-8000-" + n.ToString("D12", CultureInfo.InvariantCulture));

    private static Activity Activity(int n, Cadence cadence) => new(Id(n), $"a{n}", Unit.Reps, Floor: 5, Stretch: 8, cadence);

    private static readonly Area Area = new(Id(1), "area",
    [
        new Milestone(Id(2), "one", 3, [Activity(10, new Cadence.Daily())]),
        new Milestone(Id(3), "two", 2, [Activity(11, new Cadence.Daily())]),
    ]);

    private static readonly Curriculum Curriculum = new([Area]);
    private static readonly DateOnly Monday = new(2026, 3, 2);

    [Fact]
    public void MilestoneAt_walks_the_milestones_in_order()
    {
        foreach (var k in new[] { 0, 1, 2 }) Schedule.MilestoneAt(Area, k)?.Id.ShouldBe(Id(2));
        foreach (var k in new[] { 3, 4 }) Schedule.MilestoneAt(Area, k)?.Id.ShouldBe(Id(3));
        Schedule.MilestoneAt(Area, 5).ShouldBeNull();
    }

    [Fact]
    public void DueActivities_follows_the_milestone_covering_k()
    {
        Schedule.DueActivities(Curriculum, 2, Monday).ShouldBe([new DueActivity(Id(1), Area.Milestones[0].Activities[0], Id(2))]);
        Schedule.DueActivities(Curriculum, 3, Monday).Select(d => d.MilestoneId).ShouldBe([Id(3)]);
        Schedule.DueActivities(Curriculum, 5, Monday).ShouldBeEmpty();
    }

    [Fact]
    public void A_weekly_activity_is_due_only_on_its_weekdays()
    {
        var weekly = new Curriculum([Area with { Milestones = [new Milestone(Id(2), "w", 3, [Activity(10, new Cadence.Weekly([1, 3, 5]))])] }]);
        Schedule.DueActivities(weekly, 0, Monday).Count.ShouldBe(1);
        Schedule.DueActivities(weekly, 0, Monday.AddDays(1)).ShouldBeEmpty();
    }

    [Fact]
    public void Sunday_is_weekday_7()
    {
        var sundays = new Curriculum([Area with { Milestones = [new Milestone(Id(2), "w", 3, [Activity(10, new Cadence.Weekly([7]))])] }]);
        Schedule.DueActivities(sundays, 0, new DateOnly(2026, 3, 8)).Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(10, 0.8, 8)]
    [InlineData(1, 0.8, 1)]
    [InlineData(5, 0.64, 4)]
    [InlineData(1, 0.1, 1)]
    public void ScaledFloor_rounds_up_and_never_drops_below_1(int floor, double scale, int scaled) =>
        Schedule.ScaledFloor(floor, scale).ShouldBe(scaled);
}
