using FluentAssertions;
using SmartMarket.Application.StallRequests;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Tests.StallRequests;

public sealed class EligibilityCheckerTests
{
    private static readonly DateOnly RoundStarts = new(2026, 11, 1);

    private static readonly EligibilityFacts Good = new(
        HasIdentity: true,
        FoodHygieneTrained: true,
        BannedUntil: null,
        SpaceSellsFood: true,
        RoundStartsOn: RoundStarts,
        IsTrialArea: false,
        TrialAreaRequestsThisYear: 0,
        OtherOpenRequestsInPeriod: 0,
        PreviousRoundOutcomeHere: null);

    private static readonly EligibilityLimits Limits = new(TrialAreaRequestsPerYear: 1, MaxOpenRequestsInPeriod: 1, CarryOverWeight: 2);

    private static EligibilityDecision Check(EligibilityFacts facts) => EligibilityChecker.Check(facts, Limits);

    [Fact]
    public void A_vendor_meeting_every_rule_is_eligible_with_normal_weight()
    {
        Check(Good).Should().Be(new EligibilityDecision(true, null, 1));
    }

    [Fact]
    public void Selling_food_without_hygiene_training_is_ineligible_with_a_reason()
    {
        var decision = Check(Good with { FoodHygieneTrained = false });

        decision.Eligible.Should().BeFalse();
        decision.Reason.Should().Be("ยังไม่ผ่านการอบรมสุขาภิบาลอาหาร");
    }

    [Fact]
    public void Hygiene_training_is_only_required_where_food_is_sold()
    {
        Check(Good with { FoodHygieneTrained = false, SpaceSellsFood = false }).Eligible.Should().BeTrue();
    }

    [Fact]
    public void A_vendor_without_identity_is_ineligible()
    {
        Check(Good with { HasIdentity = false }).Reason.Should().Be("ไม่มีข้อมูลยืนยันตัวตน");
    }

    [Theory]
    [InlineData(2026, 11, 1, false)] // ban still covers the first day of the round
    [InlineData(2027, 1, 31, false)]
    [InlineData(2026, 10, 31, true)] // ban ends before the round starts
    public void A_ban_that_reaches_the_round_start_makes_the_vendor_ineligible(int y, int m, int d, bool eligible)
    {
        var decision = Check(Good with { BannedUntil = new DateOnly(y, m, d) });

        decision.Eligible.Should().Be(eligible);
        if (!eligible) decision.Reason.Should().StartWith("อยู่ระหว่างงดสิทธิ์ถึง");
    }

    [Fact]
    public void A_second_trial_area_request_in_the_same_year_is_ineligible()
    {
        Check(Good with { IsTrialArea = true, TrialAreaRequestsThisYear = 1 }).Reason
            .Should().Be("สมัครพื้นที่ทดลองค้าได้ 1 ครั้งต่อปี และใช้สิทธิ์ปีนี้แล้ว");
        Check(Good with { IsTrialArea = false, TrialAreaRequestsThisYear = 1 }).Eligible.Should().BeTrue();
    }

    [Fact]
    public void Another_unfinished_request_in_an_overlapping_round_is_ineligible()
    {
        Check(Good with { OtherOpenRequestsInPeriod = 1 }).Reason.Should().Be("มีคำขออื่นที่ยังไม่สิ้นสุดในช่วงรอบเดียวกัน");
    }

    [Fact]
    public void Every_failed_rule_is_listed()
    {
        Check(Good with { FoodHygieneTrained = false, HasIdentity = false }).Reason
            .Should().Be("ไม่มีข้อมูลยืนยันตัวตน · ยังไม่ผ่านการอบรมสุขาภิบาลอาหาร");
    }

    [Theory]
    [InlineData(StallRequestStatus.NotSelected, 2)]
    [InlineData(StallRequestStatus.Waitlisted, 2)] // ended on the waitlist without promotion
    [InlineData(StallRequestStatus.Won, 1)]
    [InlineData(StallRequestStatus.Confirmed, 1)]
    [InlineData(StallRequestStatus.Forfeited, 1)]
    [InlineData(StallRequestStatus.Ineligible, 1)]
    [InlineData(null, 1)]
    public void Carry_over_weight_doubles_only_after_missing_out_at_the_same_space(StallRequestStatus? previous, int weight)
    {
        Check(Good with { PreviousRoundOutcomeHere = previous }).Weight.Should().Be(weight);
    }

    [Fact]
    public void Ineligible_requests_keep_normal_weight()
    {
        Check(Good with { FoodHygieneTrained = false, PreviousRoundOutcomeHere = StallRequestStatus.NotSelected })
            .Weight.Should().Be(1);
    }
}
