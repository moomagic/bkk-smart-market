using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Allocation;

/// <summary>
/// Deterministic, auditable lottery (CLAUDE.md › Allocation algorithm, ADR-0001, ADR-0002).
/// Pure: the same input and seed always give the same result. Any change to the order in which
/// the RNG is consumed changes every past result for a seed — including the demo seed.
/// </summary>
public static class AllocationEngine
{
    /// <summary>Fixed draw order of the groups.</summary>
    private static readonly Category[] DrawOrder = [Category.Legacy, Category.Vulnerable, Category.New];

    /// <summary>Rollover cycle: unfilled seats move to the next group.</summary>
    private static Category Next(Category c) => c switch
    {
        Category.Vulnerable => Category.New,
        Category.New => Category.Legacy,
        _ => Category.Vulnerable,
    };

    /// <summary>The cycle unrolled from Vulnerable — the order shortfalls are resolved in.</summary>
    private static readonly Category[] RolloverOrder =
        [Category.Vulnerable, Next(Category.Vulnerable), Next(Next(Category.Vulnerable))];

    public static AllocationResult Allocate(AllocationInput input)
    {
        var candidates = Canonical(input.Candidates);
        var lockCodes = SortLockCodes(input.LockCodes);
        var n = lockCodes.Count;
        var rng = new Random(input.Seed);
        var members = DrawOrder.ToDictionary(g => g, g => candidates.Where(c => c.Category == g).ToList());
        var counts = DrawOrder.ToDictionary(g => g, g => members[g].Count);

        // 1–2. Seats per group, then rollover.
        var pooled = n == 1;
        var baseQuota = pooled ? DrawOrder.ToDictionary(g => g, _ => 0) : BaseQuota(n, counts, input.Rules);
        var (allocated, rolloverSources) = Rollover(baseQuota, counts);

        // 3. Draw every group to the end (the tail is the waitlist order). N=1 draws one pooled order
        //    across all groups instead: its head wins and it also decides who is promoted on forfeit.
        List<Candidate> pooledOrder = [];
        Dictionary<Category, List<Candidate>> drawn;
        if (pooled)
        {
            pooledOrder = DrawAll([.. candidates], rng);
            if (pooledOrder.Count > 0) allocated[pooledOrder[0].Category] = 1;
            drawn = DrawOrder.ToDictionary(g => g, g => pooledOrder.Where(c => c.Category == g).ToList());
        }
        else
        {
            drawn = DrawOrder.ToDictionary(g => g, g => DrawAll(members[g], rng));
        }

        // 5. Shuffle lock codes with the same RNG, then hand them out in outcome order.
        var shuffled = Shuffle(lockCodes, rng);
        var nextLock = 0;

        // 4. Winners, per-group waitlist, the rest not selected.
        var outcomes = new List<Outcome>();
        foreach (var g in DrawOrder)
        {
            var seats = allocated[g];
            var baseSeats = seats - rolloverSources[g].Count;
            for (var i = 0; i < drawn[g].Count; i++)
            {
                var c = drawn[g][i];
                if (i < seats)
                {
                    var code = shuffled[nextLock++];
                    var viaRollover = i >= baseSeats;
                    var why = pooled ? $"ได้สิทธิ์จากการจับรวมทุกกลุ่ม ({n} ที่)"
                        : viaRollover ? $"ได้สิทธิ์จากโควตาที่โอนมาจากกลุ่ม{Label(rolloverSources[g][i - baseSeats])}"
                        : $"ได้สิทธิ์ในโควตากลุ่ม{Label(g)} ({baseQuota[g]} ที่)";
                    outcomes.Add(new Outcome(c.VendorId, g, c.Weight, OutcomeKind.Won, code, null, viaRollover, $"{why} · ล็อค {code}"));
                }
                else if (i - seats < input.Rules.WaitlistPerGroup)
                {
                    var position = i - seats + 1;
                    outcomes.Add(new Outcome(c.VendorId, g, c.Weight, OutcomeKind.Waitlisted, null, position, false,
                        WaitlistReason(position, g)));
                }
                else
                {
                    outcomes.Add(new Outcome(c.VendorId, g, c.Weight, OutcomeKind.NotSelected, null, null, false,
                        $"ไม่ได้สิทธิ์ · เกินจำนวนสำรองของกลุ่ม{Label(g)} ({input.Rules.WaitlistPerGroup} คน)"));
                }
            }
        }

        return new AllocationResult(
            InputHash(candidates, n, input.Rules),
            pooled,
            [.. DrawOrder.Select(g => new QuotaLine(g, baseQuota[g], allocated[g]))],
            outcomes,
            [.. shuffled.Skip(nextLock)],
            [.. pooledOrder.Select(c => c.VendorId)]);
    }

    /// <summary>
    /// A winner forfeits: the head of the same group's waitlist takes the lock; if that waitlist is empty,
    /// follow the rollover cycle. A pooled draw (N=1) has no group quotas, so the earliest waitlisted
    /// vendor in the pooled order takes it instead. With nobody waiting anywhere the lock stays vacant.
    /// </summary>
    public static PromotionResult Promote(AllocationResult result, Guid forfeitedVendorId)
    {
        var forfeiter = result.Outcomes.SingleOrDefault(o => o.VendorId == forfeitedVendorId);
        if (forfeiter is not { Kind: OutcomeKind.Won, LockCode: { } lockCode })
            throw new InvalidOperationException("Only a vendor who won a lock can forfeit it.");

        Outcome? head = null;
        if (result.PooledDraw)
        {
            var waiting = result.Outcomes.Where(o => o.Kind == OutcomeKind.Waitlisted).ToDictionary(o => o.VendorId);
            head = result.PooledOrder.Where(waiting.ContainsKey).Select(id => waiting[id]).FirstOrDefault();
        }
        else for (var g = forfeiter.Category; head is null; g = Next(g))
        {
            head = result.Outcomes
                .Where(o => o.Category == g && o.Kind == OutcomeKind.Waitlisted)
                .MinBy(o => o.WaitlistPosition);
            if (Next(g) == forfeiter.Category) break;
        }

        var outcomes = result.Outcomes.Select(o =>
        {
            if (o == forfeiter)
                return o with { Kind = OutcomeKind.Forfeited, LockCode = null, Reason = "สละสิทธิ์" };
            if (o == head)
                return o with
                {
                    Kind = OutcomeKind.Won, LockCode = lockCode, WaitlistPosition = null, ViaRollover = !result.PooledDraw && o.Category != forfeiter.Category,
                    Reason = $"เลื่อนจากสำรองลำดับ {o.WaitlistPosition} · กลุ่ม{Label(o.Category)} แทนผู้สละสิทธิ์ · ล็อค {lockCode}",
                };
            if (head is not null && o.Category == head.Category && o.Kind == OutcomeKind.Waitlisted)
                return o with { WaitlistPosition = o.WaitlistPosition - 1, Reason = WaitlistReason(o.WaitlistPosition!.Value - 1, o.Category) };
            return o;
        }).ToList();

        var unassigned = head is null ? [.. result.UnassignedLockCodes, lockCode] : result.UnassignedLockCodes;
        return new PromotionResult(result with { Outcomes = outcomes, UnassignedLockCodes = unassigned }, head?.VendorId);
    }

    /// <summary>
    /// A request found eligible by a Corrected review after approval (ADR-0005): it takes a lock left vacant, if any
    /// (lowest code first), otherwise the end of its group's waitlist — past the waitlist cap if need be, since it was
    /// wrongly kept out of the draw. Winners already decided stay as they are.
    /// </summary>
    public static (AllocationResult Result, Outcome Admitted) AdmitCorrected(AllocationResult result, Guid vendorId, Category category, int weight)
    {
        if (result.Outcomes.Any(o => o.VendorId == vendorId))
            throw new InvalidOperationException("The vendor is already in this result.");

        Outcome admitted;
        var vacant = SortLockCodes(result.UnassignedLockCodes);
        if (vacant.Count > 0)
        {
            var code = vacant[0];
            admitted = new Outcome(vendorId, category, weight, OutcomeKind.Won, code, null, false,
                $"ได้ล็อคที่ว่างอยู่จากผลทบทวน (แก้ไขผลตรวจสิทธิ) · ล็อค {code}");
        }
        else
        {
            var position = result.Outcomes
                .Where(o => o.Category == category && o.Kind == OutcomeKind.Waitlisted)
                .Select(o => o.WaitlistPosition ?? 0).DefaultIfEmpty(0).Max() + 1;
            admitted = new Outcome(vendorId, category, weight, OutcomeKind.Waitlisted, null, position, false,
                $"เข้าท้ายสำรองจากผลทบทวน (แก้ไขผลตรวจสิทธิ) · {WaitlistReason(position, category)}");
        }

        return (result with
        {
            Outcomes = [.. result.Outcomes, admitted],
            UnassignedLockCodes = [.. result.UnassignedLockCodes.Where(c => c != admitted.LockCode)],
            // A pooled draw (N=1) promotes in pooled order; without this the vendor could never move up.
            PooledOrder = result.PooledDraw ? [.. result.PooledOrder, vendorId] : result.PooledOrder,
        }, admitted);
    }

    private static Dictionary<Category, int> BaseQuota(int n, IReadOnlyDictionary<Category, int> counts, RulesSnapshot rules)
    {
        if (n == 2)
        {
            // ADR-0001: 1 Vulnerable + 1 New-or-Legacy (New when both applied).
            var second = counts[Category.New] > 0 || counts[Category.Legacy] == 0 ? Category.New : Category.Legacy;
            return DrawOrder.ToDictionary(g => g, g => g == Category.Vulnerable || g == second ? 1 : 0);
        }
        if (n < 2) return DrawOrder.ToDictionary(g => g, _ => 0);

        var vulnerable = (int)Math.Ceiling(n * rules.VulnerableMin);
        var @new = (int)Math.Ceiling(n * rules.NewMin);
        var legacy = Math.Min(Math.Max(0, n - vulnerable - @new), (int)Math.Floor(n * rules.LegacyMax));
        @new += Math.Max(0, n - vulnerable - @new - legacy); // leftover seats go to New
        return new() { [Category.Legacy] = legacy, [Category.Vulnerable] = vulnerable, [Category.New] = @new };
    }

    /// <returns>Seats filled per group, and for each group the source group of every seat it received by rollover.</returns>
    private static (Dictionary<Category, int>, Dictionary<Category, List<Category>>) Rollover(
        IReadOnlyDictionary<Category, int> quota, IReadOnlyDictionary<Category, int> counts)
    {
        var allocated = DrawOrder.ToDictionary(g => g, g => Math.Min(quota[g], counts[g]));
        var sources = DrawOrder.ToDictionary(g => g, _ => new List<Category>());
        foreach (var from in RolloverOrder)
        {
            var shortfall = quota[from] - allocated[from];
            for (var to = Next(from); shortfall > 0 && to != from; to = Next(to))
            {
                var take = Math.Min(shortfall, counts[to] - allocated[to]);
                allocated[to] += take;
                sources[to].AddRange(Enumerable.Repeat(from, take));
                shortfall -= take;
            }
        }
        return (allocated, sources);
    }

    /// <summary>Weighted sampling without replacement until the pool is empty.</summary>
    private static List<Candidate> DrawAll(List<Candidate> pool, Random rng)
    {
        var order = new List<Candidate>(pool.Count);
        while (pool.Count > 0)
        {
            var pick = DrawOne(pool, rng);
            pool.Remove(pick);
            order.Add(pick);
        }
        return order;
    }

    private static Candidate DrawOne(IReadOnlyList<Candidate> pool, Random rng)
    {
        var ticket = rng.Next(pool.Sum(c => c.Weight));
        foreach (var c in pool)
        {
            if (ticket < c.Weight) return c;
            ticket -= c.Weight;
        }
        throw new InvalidOperationException("unreachable");
    }

    private static List<string> Shuffle(IReadOnlyList<string> items, Random rng)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    /// <summary>Eligible candidates only, sorted by VendorId so insertion order never matters.</summary>
    private static List<Candidate> Canonical(IReadOnlyList<Candidate> candidates)
    {
        var duplicate = candidates.GroupBy(c => c.VendorId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Vendor {duplicate.Key} appears more than once.", nameof(candidates));
        if (candidates.FirstOrDefault(c => c.Weight < 1) is { } bad)
            throw new ArgumentException($"Vendor {bad.VendorId} has weight {bad.Weight}; weights must be ≥ 1.", nameof(candidates));

        return [.. candidates.Where(c => c.Eligible).OrderBy(c => Key(c.VendorId), StringComparer.Ordinal)];
    }

    /// <summary>"B2" before "B10".</summary>
    private static List<string> SortLockCodes(IReadOnlyList<string> codes)
    {
        if (codes.Distinct().Count() != codes.Count)
            throw new ArgumentException("Lock codes must be unique.", nameof(codes));
        return [.. codes.OrderBy(c => c.Length).ThenBy(c => c, StringComparer.Ordinal)];
    }

    /// <summary>SHA256 of canonical JSON {requests:[[VendorId, Category, Weight]…], n, rules} (ADR-0002).</summary>
    private static string InputHash(IReadOnlyList<Candidate> candidates, int n, RulesSnapshot rules)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartArray("requests");
            foreach (var c in candidates)
            {
                json.WriteStartArray();
                json.WriteStringValue(Key(c.VendorId));
                json.WriteStringValue(c.Category.ToString());
                json.WriteNumberValue(c.Weight);
                json.WriteEndArray();
            }
            json.WriteEndArray();
            json.WriteNumber("n", n);
            json.WriteStartObject("rules");
            WriteDecimal(json, "legacyMax", rules.LegacyMax);
            WriteDecimal(json, "vulnerableMin", rules.VulnerableMin);
            WriteDecimal(json, "newMin", rules.NewMin);
            json.WriteNumber("waitlistPerGroup", rules.WaitlistPerGroup);
            json.WriteEndObject();
            json.WriteEndObject();
        }
        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    /// <summary>0.50 and 0.5 must hash the same.</summary>
    private static void WriteDecimal(Utf8JsonWriter json, string name, decimal value)
    {
        json.WritePropertyName(name);
        json.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }

    private static string Key(Guid vendorId) => vendorId.ToString("D");

    private static string WaitlistReason(int position, Category g) => $"สำรองลำดับ {position} · กลุ่ม{Label(g)}";

    private static string Label(Category g) => g switch
    {
        Category.Legacy => "ผู้ค้าเดิม",
        Category.Vulnerable => "เปราะบาง",
        _ => "ผู้ค้าใหม่",
    };
}
