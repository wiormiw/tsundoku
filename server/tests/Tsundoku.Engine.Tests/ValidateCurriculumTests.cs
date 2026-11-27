namespace Tsundoku.Engine.Tests;

public sealed class ValidateCurriculumTests
{
    private static Guid NewId() => Guid.CreateVersion7();

    private static Activity Act(int floor = 2, int stretch = 5, Guid? id = null) =>
        new(id ?? NewId(), "Read", Unit.Pages, floor, stretch, new Cadence.Daily());

    private static Milestone Mile(int days, params Activity[] activities) =>
        new(NewId(), "M", days, activities.Length == 0 ? [Act()] : activities);

    private static Area Area(IReadOnlyList<Milestone> milestones, Guid? id = null) => new(id ?? NewId(), "A", milestones);

    private static GoalHeader Header(IEnumerable<Area> areas, int range = 30) => new([.. areas.Select(a => a.Id)], range);

    private static IReadOnlyList<CurriculumIssue> Check(Area[] areas, GoalHeader header) =>
        CurriculumValidation.Validate(new Curriculum(areas), header);

    [Fact]
    public void Codes_equal_the_TS_list() =>
        CurriculumIssueCodes.All.ShouldBe(
            ["curriculum.area_mismatch", "curriculum.duplicate_milestone", "curriculum.duplicate_activity", "curriculum.floor_exceeds_stretch", "curriculum.range_not_covered"]);

    [Fact]
    public void A_covering_curriculum_of_exactly_the_goal_areas_has_no_issues()
    {
        Area[] areas = [Area([Mile(20), Mile(10)]), Area([Mile(30)])];
        Check(areas, Header(areas)).ShouldBeEmpty();
    }

    [Fact]
    public void A_missing_and_an_unknown_area_are_each_one_area_mismatch()
    {
        Area a = Area([Mile(30)]), b = Area([Mile(30)]), stranger = Area([Mile(30)]);
        var issues = Check([a, stranger], Header([a, b]));
        issues.Select(i => (i.Code, i.AreaId)).ShouldBe([(CurriculumIssueCodes.AreaMismatch, (Guid?)b.Id), (CurriculumIssueCodes.AreaMismatch, stranger.Id)]);
    }

    [Fact]
    public void The_same_area_twice_is_an_area_mismatch()
    {
        var a = Area([Mile(30)]);
        Check([a, Area([Mile(30)], a.Id)], Header([a])).Select(i => i.Code).ShouldBe([CurriculumIssueCodes.AreaMismatch]);
    }

    [Fact]
    public void An_activity_id_repeated_across_areas_is_one_duplicate_activity()
    {
        var shared = Act();
        Area[] areas = [Area([Mile(30, shared)]), Area([Mile(30, shared with { })])];
        var issue = Check(areas, Header(areas)).ShouldHaveSingleItem();
        (issue.Code, issue.Id).ShouldBe((CurriculumIssueCodes.DuplicateActivity, (Guid?)shared.Id));
    }

    [Fact]
    public void A_milestone_id_repeated_is_one_duplicate_milestone()
    {
        var m = Mile(15);
        Area[] areas = [Area([m, m with { Activities = [Act()] }])];
        var issue = Check(areas, Header(areas)).ShouldHaveSingleItem();
        (issue.Code, issue.Id).ShouldBe((CurriculumIssueCodes.DuplicateMilestone, (Guid?)m.Id));
    }

    [Fact]
    public void An_area_short_of_the_range_is_range_not_covered()
    {
        Area a = Area([Mile(10), Mile(19)]), ok = Area([Mile(30)]);
        var issue = Check([a, ok], Header([a, ok])).ShouldHaveSingleItem();
        (issue.Code, issue.AreaId).ShouldBe((CurriculumIssueCodes.RangeNotCovered, (Guid?)a.Id));
    }

    [Fact]
    public void An_area_with_no_milestones_is_range_not_covered()
    {
        var a = Area([]);
        Check([a], Header([a])).Select(i => i.Code).ShouldBe([CurriculumIssueCodes.RangeNotCovered]);
    }

    [Fact]
    public void A_floor_above_the_stretch_is_floor_exceeds_stretch()
    {
        var bad = Act(floor: 9, stretch: 3);
        Area[] areas = [Area([Mile(30, bad)])];
        var issue = Check(areas, Header(areas)).ShouldHaveSingleItem();
        (issue.Code, issue.Id).ShouldBe((CurriculumIssueCodes.FloorExceedsStretch, (Guid?)bad.Id));
    }

    [Fact]
    public void Issues_from_several_rules_are_all_reported()
    {
        var dup = Act();
        Area[] areas = [Area([Mile(5, dup, dup)])];
        Check(areas, Header(areas)).Select(i => i.Code).ShouldBe([CurriculumIssueCodes.DuplicateActivity, CurriculumIssueCodes.RangeNotCovered]);
    }
}
