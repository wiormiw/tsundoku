namespace Tsundoku.Engine;

/// <summary>The issue codes, equal to the TS <c>CURRICULUM_ISSUE_CODES</c>.</summary>
public static class CurriculumIssueCodes
{
    public const string AreaMismatch = "curriculum.area_mismatch";
    public const string DuplicateMilestone = "curriculum.duplicate_milestone";
    public const string DuplicateActivity = "curriculum.duplicate_activity";
    public const string FloorExceedsStretch = "curriculum.floor_exceeds_stretch";
    public const string RangeNotCovered = "curriculum.range_not_covered";

    public static IReadOnlyList<string> All { get; } = [AreaMismatch, DuplicateMilestone, DuplicateActivity, FloorExceedsStretch, RangeNotCovered];
}
