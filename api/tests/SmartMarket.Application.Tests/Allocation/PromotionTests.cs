using FluentAssertions;
using SmartMarket.Application.Allocation;
using static SmartMarket.Application.Tests.Allocation.AllocationEngineTests;
using static SmartMarket.Domain.StallRequests.Category;

namespace SmartMarket.Application.Tests.Allocation;

public sealed class PromotionTests
{
    [Fact]
    public void Forfeit_promotes_the_head_of_the_same_groups_waitlist_into_the_freed_lock()
    {
        var result = Allocate(4, Applicants(5, 5, 5));
        var forfeiter = result.Outcomes.First(o => o.Category == Vulnerable && o.Kind == OutcomeKind.Won);
        var head = result.Outcomes.Single(o => o.Category == Vulnerable && o.WaitlistPosition == 1);

        var promotion = AllocationEngine.Promote(result, forfeiter.VendorId);

        promotion.PromotedVendorId.Should().Be(head.VendorId);
        var after = promotion.Result.Outcomes.ToDictionary(o => o.VendorId);
        after[forfeiter.VendorId].Kind.Should().Be(OutcomeKind.Forfeited);
        after[forfeiter.VendorId].LockCode.Should().BeNull();
        after[head.VendorId].Kind.Should().Be(OutcomeKind.Won);
        after[head.VendorId].LockCode.Should().Be(forfeiter.LockCode);
        after[head.VendorId].Reason.Should().Contain("เลื่อนจากสำรองลำดับ 1");
        WinnersPer(promotion.Result).Should().Equal(WinnersPer(result), "quota is preserved");
        promotion.Result.Outcomes.Where(o => o.Category == Vulnerable && o.Kind == OutcomeKind.Waitlisted)
            .Select(o => o.WaitlistPosition).Should().Equal(1, 2);
    }

    [Fact]
    public void Forfeit_falls_back_along_the_rollover_cycle_when_the_group_has_no_waitlist()
    {
        // Vulnerable has exactly its one seat; next in the cycle is New.
        var result = Allocate(4, Applicants(5, 1, 5));
        var forfeiter = result.Outcomes.Single(o => o.Category == Vulnerable);
        var newHead = result.Outcomes.Single(o => o.Category == New && o.WaitlistPosition == 1);

        var promotion = AllocationEngine.Promote(result, forfeiter.VendorId);

        promotion.PromotedVendorId.Should().Be(newHead.VendorId);
        promotion.Result.Outcomes.Single(o => o.VendorId == newHead.VendorId).LockCode.Should().Be(forfeiter.LockCode);
    }

    [Fact]
    public void Forfeit_with_nobody_waiting_leaves_the_lock_vacant()
    {
        var result = Allocate(4, Applicants(1, 1, 1));
        var forfeiter = result.Outcomes.First(o => o.Kind == OutcomeKind.Won);

        var promotion = AllocationEngine.Promote(result, forfeiter.VendorId);

        promotion.PromotedVendorId.Should().BeNull();
        promotion.Result.UnassignedLockCodes.Should().Contain(forfeiter.LockCode!);
    }

    [Fact]
    public void Only_a_winner_can_forfeit()
    {
        var result = Allocate(4, Applicants(5, 5, 5));
        var waitlisted = result.Outcomes.First(o => o.Kind == OutcomeKind.Waitlisted);

        var act = () => AllocationEngine.Promote(result, waitlisted.VendorId);

        act.Should().Throw<InvalidOperationException>();
    }
}
