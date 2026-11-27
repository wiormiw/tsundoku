namespace Tsundoku.Engine;

/// <summary>One broken rule; <c>AreaId</c> or <c>Id</c> (a milestone or an activity) says where.</summary>
public sealed record CurriculumIssue(string Code, string Message, Guid? AreaId = null, Guid? Id = null);
