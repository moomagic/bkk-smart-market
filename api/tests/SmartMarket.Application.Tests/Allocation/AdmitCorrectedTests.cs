using FluentAssertions;
using SmartMarket.Application.Allocation;
using static SmartMarket.Application.Tests.Allocation.AllocationEngineTests;
using static SmartMarket.Domain.StallRequests.Category;

namespace SmartMarket.Application.Tests.Allocation;

public sealed class AdmitCorrectedTests
{
    private static readonly Guid Late = new("99999999-0000-0000-0000-000000000001");

    [Fact]
    public void Takes_the_lowest_vacant_lock_and_leaves_the_winners_alone()
    {
        var result = Allocate(4, Applicants(1, 0, 1));
        result.UnassignedLockCodes.Should().HaveCount(2);

        var (after, admitted) = AllocationEngine.AdmitCorrected(result, Late, New, 1);

        admitted.Kind.Should().Be(OutcomeKind.Won);
        admitted.LockCode.Should().Be(result.UnassignedLockCodes.OrderBy(c => c).First());
        after.UnassignedLockCodes.Should().ContainSingle().Which.Should().NotBe(admitted.LockCode);
        after.Outcomes.Where(o => o.VendorId != Late).Should().BeEquivalentTo(result.Outcomes, "winners stay as they are");
    }

    [Fact]
    public void Without_a_vacant_lock_joins_the_end_of_its_groups_waitlist_even_past_the_cap()
    {
        var result = Allocate(4, Applicants(5, 5, 5));
        result.Outcomes.Count(o => o.Category == New && o.Kind == OutcomeKind.Waitlisted).Should().Be(3);

        var (after, admitted) = AllocationEngine.AdmitCorrected(result, Late, New, 2);

        admitted.Should().Match<Outcome>(o => o.Kind == OutcomeKind.Waitlisted && o.WaitlistPosition == 4 && o.Weight == 2);
        after.Outcomes.Should().HaveCount(result.Outcomes.Count + 1);
        after.UnassignedLockCodes.Should().BeEmpty();
    }

    [Fact]
    public void In_a_pooled_draw_the_vendor_is_added_to_the_pooled_order_so_a_forfeit_can_reach_them()
    {
        var result = Allocate(1, Applicants(1, 0, 0));

        var (after, _) = AllocationEngine.AdmitCorrected(result, Late, Vulnerable, 1);
        var winner = after.Outcomes.Single(o => o.Kind == OutcomeKind.Won);

        after.PooledOrder.Should().EndWith(Late);
        AllocationEngine.Promote(after, winner.VendorId).PromotedVendorId.Should().Be(Late);
    }
}
