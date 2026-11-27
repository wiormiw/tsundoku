namespace Tsundoku.Engine;

public static class CurriculumValidation
{
    /// <summary>
    /// The structural rules of a curriculum against its goal; empty when it is sound. Prerequisites are sequential
    /// (milestones are ordered per area), so there is no graph to check. A floor above the stretch is checked for
    /// data that never passed through the schema.
    /// </summary>
    public static IReadOnlyList<CurriculumIssue> Validate(Curriculum curriculum, GoalHeader goal)
    {
        ArgumentNullException.ThrowIfNull(curriculum);
        ArgumentNullException.ThrowIfNull(goal);
        var issues = new List<CurriculumIssue>();
        var areas = curriculum.Areas;

        var wanted = goal.AreaIds.ToHashSet();
        var covered = areas.Select(a => a.Id).ToHashSet();
        foreach (var id in goal.AreaIds)
        {
            if (!covered.Contains(id))
                issues.Add(new(CurriculumIssueCodes.AreaMismatch, "The goal has an area the curriculum does not cover.", AreaId: id));
        }
        var seenAreas = new HashSet<Guid>();
        foreach (var area in areas)
        {
            var known = wanted.Contains(area.Id);
            if (!known || !seenAreas.Add(area.Id))
            {
                issues.Add(new(CurriculumIssueCodes.AreaMismatch,
                    known ? "The curriculum lists this area more than once." : "The curriculum has an area the goal does not have.",
                    AreaId: area.Id));
            }
        }

        var milestoneIds = new HashSet<Guid>();
        foreach (var milestone in areas.SelectMany(a => a.Milestones))
        {
            if (!milestoneIds.Add(milestone.Id))
                issues.Add(new(CurriculumIssueCodes.DuplicateMilestone, "A milestone id is used more than once.", Id: milestone.Id));
        }

        var activities = areas.SelectMany(a => a.Milestones).SelectMany(m => m.Activities).ToList();
        var activityIds = new HashSet<Guid>();
        foreach (var activity in activities)
        {
            if (!activityIds.Add(activity.Id))
                issues.Add(new(CurriculumIssueCodes.DuplicateActivity, "An activity id is used more than once.", Id: activity.Id));
        }
        foreach (var activity in activities)
        {
            if (activity.Floor > activity.Stretch)
                issues.Add(new(CurriculumIssueCodes.FloorExceedsStretch, "An activity floor is above its stretch.", Id: activity.Id));
        }

        foreach (var area in areas)
        {
            var days = area.Milestones.Sum(m => m.Days);
            if (days < goal.RangeActiveDays)
            {
                issues.Add(new(CurriculumIssueCodes.RangeNotCovered,
                    $"The milestones cover {days} active days; the goal needs {goal.RangeActiveDays}.", AreaId: area.Id));
            }
        }
        return issues;
    }
}
