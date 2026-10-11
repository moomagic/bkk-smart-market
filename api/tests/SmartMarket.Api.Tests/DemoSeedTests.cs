using FluentAssertions;
using SmartMarket.Application.Allocation;
using SmartMarket.Domain.StallRequests;
using SmartMarket.Infrastructure.Allocation;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Api.Tests;

/// <summary>Pins the demo seed (สมศรี ใ. → B2) and the limits ADR-0002 puts on it.</summary>
public sealed class DemoSeedTests
{
    [Fact]
    public void Demo_seed_gives_somsri_lock_B2_when_she_submits_as_the_11th()
    {
        var result = AllocationEngine.Allocate(new AllocationInput(
            [.. SeedData.Locks.Where(l => l.SpaceId == SeedData.PakSoiCharan79).Select(l => l.Code)],
            CharanCandidatesAfterEligibility(), SeedData.CharanDemoSeed, new RulesSnapshot(0.50m, 0.25m, 0.25m, 3)));

        result.Outcomes.Single(o => o.VendorId == SeedData.Somsri)
            .Should().Match<Outcome>(o => o.Kind == OutcomeKind.Won && o.LockCode == "B2");
    }

    [Fact]
    public void Demo_seed_is_used_only_for_the_first_run_of_the_demo_round()
    {
        var seeds = new DemoSeedSource(new CryptoSeedSource());

        seeds.NextSeed(SeedData.CharanCurrentRound, priorRuns: 0).Should().Be(SeedData.CharanDemoSeed);
        Enumerable.Range(0, 5).Select(_ => seeds.NextSeed(SeedData.CharanCurrentRound, priorRuns: 1))
            .Should().NotContain(SeedData.CharanDemoSeed, "a re-run after reject gets a fresh seed");
        Enumerable.Range(0, 5).Select(_ => seeds.NextSeed(SeedData.MrtCurrentRound, priorRuns: 0))
            .Should().NotContain(SeedData.CharanDemoSeed);
    }

    /// <summary>
    /// The seeded ปากซอยจรัญฯ 79 requests plus สมศรี ใ. (Legacy), as eligibility leaves them: no hygiene training →
    /// ineligible; NotSelected here last round → ×2. Derived from SeedData so a seed-data change breaks this pin.
    /// </summary>
    private static List<Candidate> CharanCandidatesAfterEligibility()
    {
        var vendors = SeedData.Vendors.ToDictionary(v => v.Id);
        var missedOut = SeedData.StallRequests
            .Where(q => q.RoundId == SeedData.CharanPreviousRound && q.Status == StallRequestStatus.NotSelected)
            .Select(q => q.VendorId).ToHashSet();
        return [.. SeedData.StallRequests
            .Where(q => q.RoundId == SeedData.CharanCurrentRound)
            .Select(q => (q.VendorId, q.Category))
            .Append((VendorId: SeedData.Somsri, Category: Category.Legacy))
            .Select(x => new Candidate(x.VendorId, x.Category, missedOut.Contains(x.VendorId) ? 2 : 1,
                vendors[x.VendorId].FoodHygieneTrained))];
    }
}
