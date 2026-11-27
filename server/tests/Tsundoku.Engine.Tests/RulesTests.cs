namespace Tsundoku.Engine.Tests;

public sealed class RulesTests
{
    [Fact]
    public void Default_holds_the_spec_values_exactly() =>
        Rules.Default.ShouldBe(new Rules
        {
            BaseMultiplier = 1.25,
            DueWindowDays = 2,
            DailyCapRatio = 1.5,
            EscalationStep = 0.1,
            MultiplierMax = 2.0,
            BaseMin = 1.1,
            BaseMax = 1.75,
            TuneStep = 0.05,
            BankruptcyFloorRatio = 3,
            BankruptcyReplanScale = 0.8,
        });

    // The literal is the output of the TS rulesKey(DEFAULT_RULES): a snapshot written by either engine reads in the other.
    [Fact]
    public void Key_of_the_default_rules_is_the_string_the_TS_engine_writes() =>
        Rules.Key(Rules.Default).ShouldBe(
            "bankruptcyFloorRatio=3;bankruptcyReplanScale=0.8;baseMax=1.75;baseMin=1.1;baseMultiplier=1.25;"
            + "dailyCapRatio=1.5;dueWindowDays=2;escalationStep=0.1;multiplierMax=2;tuneStep=0.05");

    [Fact]
    public void Key_changes_with_any_rule() =>
        Rules.Key(Rules.Default with { BaseMax = 1.35 }).ShouldContain("baseMax=1.35;");
}
