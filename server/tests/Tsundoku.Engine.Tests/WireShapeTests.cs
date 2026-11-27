using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tsundoku.Engine.Tests;

/// <summary>The records read the TS wire shape strictly: camelCase, discriminated by <c>type</c> and <c>kind</c>, nothing unknown.</summary>
public sealed class WireShapeTests
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private static string Envelope(string type, string data) => $$"""
        {"type":"{{type}}","id":"00000000-0000-4000-8000-000000000001","goalId":null,"seq":"101","v":1,
         "occurredAt":"2026-03-02T05:00:00Z","receivedAt":"2026-03-02T05:00:01Z","data":{{data}}}
        """;

    [Fact]
    public void A_plan_reads_with_its_cadences_and_units()
    {
        var fact = JsonSerializer.Deserialize<Fact>(Envelope("plan.approved", """
            {"planVersion":1,"curriculum":{"areas":[{"id":"00000000-0000-4000-8000-0000000000a1","name":"Practice","milestones":[
              {"id":"00000000-0000-4000-8000-0000000000c1","title":"Core","days":100,"activities":[
                {"id":"00000000-0000-4000-8000-0000000000b1","name":"Act1","unit":"minutes","floor":40,"stretch":80,"cadence":{"kind":"daily"}},
                {"id":"00000000-0000-4000-8000-0000000000b2","name":"Act2","unit":"done","floor":1,"stretch":1,"cadence":{"kind":"weekly","weekdays":[1]}}]}]}]}}
            """), Strict);

        var plan = fact.ShouldBeOfType<Fact.PlanApproved>();
        plan.Seq.ShouldBe(101);
        var activities = plan.Data.Curriculum.Areas[0].Milestones[0].Activities;
        activities[0].Unit.ShouldBe(Unit.Minutes);
        activities[0].Cadence.ShouldBeOfType<Cadence.Daily>();
        activities[1].Cadence.ShouldBeOfType<Cadence.Weekly>().Weekdays.ShouldBe([1]);
    }

    [Fact]
    public void Optional_fields_may_be_absent() =>
        JsonSerializer.Deserialize<Fact>(Envelope("goal.created", """
            {"title":"Goal","areas":[{"id":"00000000-0000-4000-8000-0000000000a1","name":"Practice"}],
             "rangeActiveDays":30,"startDate":"2026-03-02","timezone":"Asia/Jakarta","caps":{}}
            """), Strict).ShouldBeOfType<Fact.GoalCreated>().Data.Caps.OfflinePerMonth.ShouldBeNull();

    [Theory]
    [InlineData("habit.formed", """{}""")]
    [InlineData("retro.ended", """{"extra":1}""")]
    [InlineData("debt.rated", """{"areaId":"00000000-0000-4000-8000-0000000000a1","date":"2026-03-02","rating":"Hard"}""")]
    [InlineData("debt.rated", """{"areaId":"00000000-0000-4000-8000-0000000000a1","date":"2026-03-02","rating":2}""")]
    [InlineData("activity.logged", """{"amount":1}""")]
    public void Anything_off_the_contract_fails(string type, string data) =>
        Should.Throw<Exception>(() => JsonSerializer.Deserialize<Fact>(Envelope(type, data), Strict));
}
