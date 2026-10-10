using System.Text.Json;
using FluentAssertions;
using SmartMarket.Application.Allocation;
using SmartMarket.Domain.StallRequests;
using static SmartMarket.Domain.StallRequests.Category;

namespace SmartMarket.Application.Tests.Allocation;

public sealed class AllocationEngineTests
{
    private static readonly RulesSnapshot Rules = new(LegacyMax: 0.50m, VulnerableMin: 0.25m, NewMin: 0.25m, WaitlistPerGroup: 3);

    // ---- quota (CLAUDE.md › Allocation algorithm step 1, ADR-0001) ----

    [Theory]
    [InlineData(2, 0, 1, 1)]
    [InlineData(3, 1, 1, 1)]
    [InlineData(4, 2, 1, 1)]
    [InlineData(5, 1, 2, 2)]
    [InlineData(6, 2, 2, 2)]
    [InlineData(7, 3, 2, 2)]
    [InlineData(8, 4, 2, 2)]
    [InlineData(9, 3, 3, 3)]
    [InlineData(10, 4, 3, 3)]
    public void Quota_with_enough_applicants_in_every_group(int n, int legacy, int vulnerable, int @new)
    {
        var result = Allocate(n, Applicants(10, 10, 10));

        WinnersPer(result).Should().Equal(legacy, vulnerable, @new);
        result.Quota.Select(q => q.Base).Should().Equal(legacy, vulnerable, @new);
    }

    [Fact]
    public void Legacy_exceeds_half_only_through_rollover()
    {
        foreach (var n in Enumerable.Range(1, 10))
        foreach (var (l, v, nw) in from l in Enumerable.Range(0, 7) from v in Enumerable.Range(0, 7) from nw in Enumerable.Range(0, 7) select (l, v, nw))
        {
            var legacyOnOwnQuota = Allocate(n, Applicants(l, v, nw)).Outcomes
                .Count(o => o.Category == Legacy && o.Kind == OutcomeKind.Won && !o.ViaRollover);
            if (n > 1) legacyOnOwnQuota.Should().BeLessThanOrEqualTo(n / 2, $"N={n}, applicants {l}/{v}/{nw}");
        }
    }

    [Fact]
    public void N1_is_a_single_draw_pooled_across_all_groups()
    {
        var winners = Enumerable.Range(0, 300)
            .Select(seed => Allocate(1, Applicants(1, 1, 1), seed))
            .Select(r => r.Outcomes.Single(o => o.Kind == OutcomeKind.Won).Category)
            .ToList();

        Allocate(1, Applicants(1, 1, 1)).PooledDraw.Should().BeTrue();
        winners.Distinct().Should().BeEquivalentTo([Legacy, Vulnerable, New], "every group can win the single lock");
    }

    [Theory]
    [InlineData(3, 3, 0, 1, 1, 0)] // no New → second lock to Legacy
    [InlineData(3, 0, 3, 0, 0, 2)] // no Vulnerable → its lock rolls to New
    [InlineData(0, 3, 0, 0, 2, 0)] // only Vulnerable → both locks
    public void N2_special_rule(int l, int v, int nw, int legacy, int vulnerable, int @new)
    {
        WinnersPer(Allocate(2, Applicants(l, v, nw))).Should().Equal(legacy, vulnerable, @new);
    }

    // ---- rollover (step 2): Vulnerable → New → Legacy → Vulnerable ----

    [Theory]
    [InlineData(4, 5, 0, 5, 2, 0, 2)] // no Vulnerable → their lock goes to New
    [InlineData(4, 5, 5, 0, 3, 1, 0)] // no New → Legacy (75%, allowed only via rollover)
    [InlineData(4, 0, 5, 5, 0, 3, 1)] // no Legacy → Vulnerable
    [InlineData(4, 5, 0, 0, 4, 0, 0)] // only Legacy applied
    [InlineData(4, 0, 0, 6, 0, 0, 4)] // everyone in one group
    [InlineData(4, 2, 5, 0, 2, 2, 0)] // New short, Legacy full → skips to Vulnerable
    [InlineData(8, 5, 1, 5, 4, 1, 3)] // Vulnerable partly short
    [InlineData(4, 2, 0, 0, 2, 0, 0)] // fewer applicants than locks → locks stay empty
    [InlineData(4, 0, 5, 0, 0, 4, 0)] // only Vulnerable applied
    [InlineData(4, 0, 1, 5, 0, 1, 3)] // Legacy short, Vulnerable full → passes on to New
    [InlineData(8, 5, 1, 1, 5, 1, 1)] // Vulnerable and New short together
    public void Rollover_moves_unfilled_quota_along_the_cycle(
        int n, int legacyApplicants, int vulnerableApplicants, int newApplicants,
        int legacy, int vulnerable, int @new)
    {
        var result = Allocate(n, Applicants(legacyApplicants, vulnerableApplicants, newApplicants));

        WinnersPer(result).Should().Equal(legacy, vulnerable, @new);
        result.Quota.Select(q => q.Allocated).Should().Equal(legacy, vulnerable, @new);
    }

    [Fact]
    public void Fewer_applicants_than_locks_all_win()
    {
        var result = Allocate(6, Applicants(1, 1, 1));

        result.Outcomes.Should().OnlyContain(o => o.Kind == OutcomeKind.Won);
        result.UnassignedLockCodes.Should().HaveCount(3);
    }

    [Fact]
    public void No_applicants_leaves_every_lock_unassigned()
    {
        var result = Allocate(4, []);

        result.Outcomes.Should().BeEmpty();
        result.UnassignedLockCodes.Should().BeEquivalentTo(["B1", "B2", "B3", "B4"]);
    }

    // ---- determinism (Guardrail 2) ----

    [Fact]
    public void Same_seed_and_same_input_in_any_order_gives_byte_equal_results()
    {
        var applicants = Applicants(4, 3, 6);
        var shuffled = applicants.OrderByDescending(a => a.VendorId).ToList();

        var first = JsonSerializer.SerializeToUtf8Bytes(Allocate(4, applicants, seed: 42));
        var second = JsonSerializer.SerializeToUtf8Bytes(Allocate(4, shuffled, seed: 42));

        second.Should().Equal(first);
    }

    [Fact]
    public void Different_seeds_give_different_draws()
    {
        var applicants = Applicants(4, 3, 6);
        var draws = Enumerable.Range(0, 20)
            .Select(seed => JsonSerializer.Serialize(Allocate(4, applicants, seed).Outcomes))
            .Distinct();

        draws.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Double_weight_doubles_the_odds()
    {
        // New has one seat at N=4; A (weight 2) vs B (weight 1) → P(A) = 2/3.
        var a = Candidate(101, New, weight: 2);
        var b = Candidate(102, New, weight: 1);
        var applicants = Applicants(5, 5, 0).Append(a).Append(b).ToList();

        var aWins = Enumerable.Range(0, 10_000)
            .Count(seed => Allocate(4, applicants, seed).Outcomes.Single(o => o.VendorId == a.VendorId).Kind == OutcomeKind.Won);

        // σ ≈ 0.0047 at 10,000 runs; ±0.02 is > 4σ.
        (aWins / 10_000.0).Should().BeApproximately(2.0 / 3, 0.02);
    }

    // ---- integrity ----

    [Fact]
    public void Nobody_gets_two_locks_and_every_lock_goes_to_at_most_one_vendor()
    {
        foreach (var seed in Enumerable.Range(0, 200))
        {
            var result = Allocate(10, Applicants(6, 6, 6), seed);
            var winners = result.Outcomes.Where(o => o.Kind == OutcomeKind.Won).ToList();

            result.Outcomes.Select(o => o.VendorId).Should().OnlyHaveUniqueItems();
            winners.Select(o => o.LockCode).Should().OnlyHaveUniqueItems().And.NotContainNulls();
            winners.Should().HaveCount(10);
        }
    }

    [Fact]
    public void A_vendor_listed_twice_is_rejected()
    {
        var dup = Candidate(1, New);

        var act = () => Allocate(4, [dup, dup]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ineligible_requests_are_never_drawn()
    {
        var ineligible = Enumerable.Range(100, 5).Select(i => Candidate(i, Vulnerable) with { Eligible = false });
        var applicants = Applicants(5, 1, 5).Concat(ineligible).ToList();

        foreach (var seed in Enumerable.Range(0, 100))
        {
            var result = Allocate(4, applicants, seed);
            result.Outcomes.Select(o => o.VendorId).Should().NotIntersectWith(
                applicants.Where(a => !a.Eligible).Select(a => a.VendorId));
        }
    }

    [Fact]
    public void Waitlist_is_per_group_capped_and_numbered_in_draw_order()
    {
        var result = Allocate(4, Applicants(6, 6, 6));

        foreach (var group in new[] { Legacy, Vulnerable, New })
        {
            var waitlisted = result.Outcomes.Where(o => o.Category == group && o.Kind == OutcomeKind.Waitlisted).ToList();
            waitlisted.Select(o => o.WaitlistPosition).Should().Equal(1, 2, 3);
            result.Outcomes.Count(o => o.Category == group && o.Kind == OutcomeKind.NotSelected).Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void Lock_codes_are_shuffled_not_handed_out_in_draw_order()
    {
        // Legacy is drawn first; without the shuffle Legacy would always hold B1.
        var b1Holders = Enumerable.Range(0, 200)
            .Select(seed => Allocate(4, Applicants(5, 5, 5), seed).Outcomes.Single(o => o.LockCode == "B1").Category)
            .ToList();

        b1Holders.Should().Contain(Vulnerable).And.Contain(New);
    }

    // ---- reasons ----

    [Fact]
    public void Every_outcome_carries_a_thai_reason()
    {
        var result = Allocate(4, Applicants(5, 5, 0));

        result.Outcomes.Where(o => o.Kind == OutcomeKind.Won && o.Category == Legacy && !o.ViaRollover)
            .Should().OnlyContain(o => o.Reason.StartsWith("ได้สิทธิ์ในโควตากลุ่มผู้ค้าเดิม (2 ที่)"));
        result.Outcomes.Where(o => o.ViaRollover)
            .Should().ContainSingle().Which.Reason.Should().Contain("โควตาที่โอนมาจากกลุ่มผู้ค้าใหม่");
        result.Outcomes.Where(o => o.Kind == OutcomeKind.Waitlisted && o.WaitlistPosition == 1 && o.Category == Vulnerable)
            .Should().ContainSingle().Which.Reason.Should().Be("สำรองลำดับ 1 · กลุ่มเปราะบาง");
        result.Outcomes.Where(o => o.Kind == OutcomeKind.Won).Should().OnlyContain(o => o.Reason.Contains($"ล็อค {o.LockCode}"));
    }

    // ---- input hash (ADR-0002) ----

    [Fact]
    public void Input_hash_ignores_order_and_seed_but_not_category_or_rules()
    {
        var applicants = Applicants(3, 3, 3);
        var baseline = Allocate(4, applicants, seed: 1).InputHash;

        Allocate(4, applicants.AsEnumerable().Reverse().ToList(), seed: 2).InputHash.Should().Be(baseline);
        Allocate(4, applicants.Select((a, i) => i == 0 ? a with { Category = New } : a).ToList()).InputHash.Should().NotBe(baseline);
        Allocate(4, applicants, rules: Rules with { WaitlistPerGroup = 2 }).InputHash.Should().NotBe(baseline);
        Allocate(5, applicants).InputHash.Should().NotBe(baseline);
    }

    [Fact]
    public void Input_hash_treats_equal_percentages_the_same()
    {
        Allocate(4, Applicants(3, 3, 3), rules: Rules with { LegacyMax = 0.5m }).InputHash
            .Should().Be(Allocate(4, Applicants(3, 3, 3), rules: Rules with { LegacyMax = 0.50m }).InputHash);
    }

    // ---- helpers ----

    internal static AllocationResult Allocate(int n, IReadOnlyList<Candidate> applicants, int seed = 7, RulesSnapshot? rules = null) =>
        AllocationEngine.Allocate(new AllocationInput(
            Enumerable.Range(1, n).Select(i => $"B{i}").ToList(), applicants, seed, rules ?? Rules));

    internal static int[] WinnersPer(AllocationResult result) =>
        [.. new[] { Legacy, Vulnerable, New }.Select(g => result.Outcomes.Count(o => o.Category == g && o.Kind == OutcomeKind.Won))];

    internal static Candidate Candidate(int n, Category category, int weight = 1) =>
        new(new Guid($"00000000-0000-0000-0000-{n:000000000000}"), category, weight, Eligible: true);

    /// <summary>VendorIds 1.., grouped Legacy, then Vulnerable, then New.</summary>
    internal static List<Candidate> Applicants(int legacy, int vulnerable, int @new)
    {
        var n = 0;
        return
        [
            .. Enumerable.Range(0, legacy).Select(_ => Candidate(++n, Legacy)),
            .. Enumerable.Range(0, vulnerable).Select(_ => Candidate(++n, Vulnerable)),
            .. Enumerable.Range(0, @new).Select(_ => Candidate(++n, New)),
        ];
    }
}
