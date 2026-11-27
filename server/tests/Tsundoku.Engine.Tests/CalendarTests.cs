namespace Tsundoku.Engine.Tests;

public sealed class CalendarTests
{
    private const string Jakarta = "Asia/Jakarta";

    private static DateTimeOffset At(string instant) => DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void LocalDate_rolls_over_at_local_midnight()
    {
        Calendar.LocalDate(At("2026-03-01T16:59:59Z"), Jakarta).ShouldBe(new DateOnly(2026, 3, 1));
        Calendar.LocalDate(At("2026-03-01T17:00:00Z"), Jakarta).ShouldBe(new DateOnly(2026, 3, 2));
    }

    [Fact]
    public void IsBeforeLocalMidnight_is_the_deadline_for_a_mark_on_a_date()
    {
        var day = new DateOnly(2026, 3, 2);
        Calendar.IsBeforeLocalMidnight(At("2026-03-01T16:59:59Z"), day, Jakarta).ShouldBeTrue();
        Calendar.IsBeforeLocalMidnight(At("2026-03-01T17:00:00Z"), day, Jakarta).ShouldBeFalse();
        Calendar.IsBeforeLocalMidnight(At("2026-03-02T05:00:00Z"), day, Jakarta).ShouldBeFalse();
    }

    // Santiago springs from 00:00 -04 to 01:00 -03 on 2026-09-06: that day has no midnight.
    // Temporal (the TS reference) starts the day at 01:00 -03 = 04:00Z; the values below are what it computes.
    [Fact]
    public void A_day_that_starts_at_a_DST_jump_begins_where_Temporal_begins_it()
    {
        const string santiago = "America/Santiago";
        var day = new DateOnly(2026, 9, 6);
        Calendar.LocalDate(At("2026-09-06T03:59:59Z"), santiago).ShouldBe(new DateOnly(2026, 9, 5));
        Calendar.LocalDate(At("2026-09-06T04:00:00Z"), santiago).ShouldBe(day);
        Calendar.IsBeforeLocalMidnight(At("2026-09-06T03:59:59Z"), day, santiago).ShouldBeTrue();
        Calendar.IsBeforeLocalMidnight(At("2026-09-06T04:00:00Z"), day, santiago).ShouldBeFalse();
    }

    // Havana falls back from 01:00 -04 to 00:00 -05 on 2026-11-01, so its midnight happens twice:
    // Temporal takes the earlier one, 00:00 -04 = 04:00Z.
    [Fact]
    public void A_midnight_that_happens_twice_is_the_earlier_one()
    {
        const string havana = "America/Havana";
        var day = new DateOnly(2026, 11, 1);
        Calendar.IsBeforeLocalMidnight(At("2026-11-01T03:59:59Z"), day, havana).ShouldBeTrue();
        Calendar.IsBeforeLocalMidnight(At("2026-11-01T04:00:00Z"), day, havana).ShouldBeFalse();
    }

    [Fact]
    public void MonthKey_is_year_and_month()
    {
        Calendar.MonthKey(new DateOnly(2026, 2, 9)).ShouldBe("2026-02");
        Calendar.MonthKey(new DateOnly(2026, 11, 30)).ShouldBe("2026-11");
    }

    [Theory]
    [InlineData(2, null, 9)]
    [InlineData(4, null, 10)]
    [InlineData(3, null, 10)]
    [InlineData(3, 4, 4)]
    [InlineData(3, 40, 10)]
    public void AllowanceCap_is_a_third_of_the_month_bounded_by_the_goal_cap(int month, int? goalCap, int cap) =>
        Calendar.AllowanceCap(new DateOnly(2026, month, 10), goalCap).ShouldBe(cap);

    [Fact]
    public void HasAllowanceRoom_counts_only_the_dates_month()
    {
        MonthUsage[] usage = [new("2026-02", 9), new("2026-03", 3)];
        Calendar.HasAllowanceRoom(null, usage, new DateOnly(2026, 2, 20)).ShouldBeFalse();
        Calendar.HasAllowanceRoom(null, usage, new DateOnly(2026, 3, 20)).ShouldBeTrue();
        Calendar.HasAllowanceRoom(3, usage, new DateOnly(2026, 3, 20)).ShouldBeFalse();
        Calendar.HasAllowanceRoom(0, [], new DateOnly(2026, 4, 1)).ShouldBeFalse();
    }
}
