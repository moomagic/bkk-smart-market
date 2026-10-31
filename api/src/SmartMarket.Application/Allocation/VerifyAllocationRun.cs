using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;

namespace SmartMarket.Application.Allocation;

/// <param name="VendorName">Masked name; null when the difference is not about one vendor (quota, lock order…).</param>
/// <param name="Field">kind · lockCode · waitlistPosition · outcome · quota · unassignedLockCodes · pooledOrder · order · record</param>
public sealed record RunMismatch(string? VendorName, string Field, string? Recorded, string? Replayed);

/// <param name="InputHashMatches">The input rebuilt from the draw record hashes to the run's stored InputHash.</param>
/// <param name="ResultMatches">Drawing again gives the draw record exactly.</param>
public sealed record RunVerificationDto(
    Guid RunId, string ReferenceNo, int Seed, bool InputHashMatches, bool ResultMatches, IReadOnlyList<RunMismatch> Mismatches);

/// <summary>
/// Replay verification (CONTEXT.md): draws the run again from what the run itself stores — the vendors, categories and
/// weights in its draw record, its lock codes, seed and RulesSnapshot — and compares with the draw record
/// (<see cref="Domain.Allocation.AllocationRun.ResultJson"/>), never the current result: forfeits and promotions follow
/// the rules after the draw (ADR-0004). Request rows are not read, so a later Corrected review cannot read as tampering.
/// A query: nothing changes, so no audit entry. Officers verify any run; a vendor, the runs of a round they applied
/// to — the same runs their result page lists (ADR-0002).
/// </summary>
public sealed class VerifyAllocationRunHandler(IAppDbContext db, ICurrentActor actor)
{
    public const string VendorRole = "vendor";

    public async Task<Result<RunVerificationDto>> Handle(Guid runId, CancellationToken ct)
    {
        var run = await db.AllocationRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null || (actor.Role == VendorRole && !await AppliedTo(run.RoundId, ct)))
            return Error.NotFound("allocation_run.not_found", "ไม่พบผลการจัดสรรนี้");

        var recorded = AllocationJson.Deserialize<AllocationResult>(run.ResultJson);
        var rules = AllocationJson.Deserialize<RulesSnapshot>(run.RulesSnapshot);
        var ids = recorded.Outcomes.Select(o => o.VendorId).ToList();
        var names = await db.Vendors.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.MaskedName, ct);

        AllocationResult replayed;
        try
        {
            replayed = AllocationEngine.Allocate(new AllocationInput(
                [.. recorded.Outcomes.Select(o => o.LockCode).OfType<string>(), .. recorded.UnassignedLockCodes],
                [.. recorded.Outcomes.Select(o => new Candidate(o.VendorId, o.Category, o.Weight, Eligible: true))],
                run.Seed, rules));
        }
        catch (ArgumentException)
        {
            // A record the engine cannot even read back (a lock code or vendor twice) has been altered
            return new RunVerificationDto(run.Id, run.ReferenceNo, run.Seed, false, false, [new(null, "record", null, null)]);
        }

        // Compared re-serialized: jsonb does not keep the text as written
        var resultMatches = AllocationJson.Serialize(recorded) == AllocationJson.Serialize(replayed);
        var mismatches = resultMatches ? [] : Differences(recorded, replayed, id => names.GetValueOrDefault(id));
        return new RunVerificationDto(run.Id, run.ReferenceNo, run.Seed, replayed.InputHash == run.InputHash, resultMatches, mismatches);
    }

    private async Task<bool> AppliedTo(Guid roundId, CancellationToken ct) =>
        Guid.TryParse(actor.Actor, out var vendorId)
        && await db.StallRequests.AnyAsync(q => q.RoundId == roundId && q.VendorId == vendorId, ct);

    private static List<RunMismatch> Differences(AllocationResult recorded, AllocationResult replayed, Func<Guid, string?> name)
    {
        var list = new List<RunMismatch>();
        var again = replayed.Outcomes.ToDictionary(o => o.VendorId);
        foreach (var r in recorded.Outcomes)
        {
            if (!again.TryGetValue(r.VendorId, out var p))
            {
                list.Add(new(name(r.VendorId), "outcome", r.Kind.ToString(), null));
                continue;
            }
            Add(list, name(r.VendorId), "kind", r.Kind.ToString(), p.Kind.ToString());
            Add(list, name(r.VendorId), "lockCode", r.LockCode, p.LockCode);
            Add(list, name(r.VendorId), "waitlistPosition", Text(r.WaitlistPosition), Text(p.WaitlistPosition));
        }
        Add(list, null, "quota", string.Join(" ", recorded.Quota.Select(Quota)), string.Join(" ", replayed.Quota.Select(Quota)));
        Add(list, null, "unassignedLockCodes", string.Join(" ", recorded.UnassignedLockCodes), string.Join(" ", replayed.UnassignedLockCodes));
        if (!recorded.PooledOrder.SequenceEqual(replayed.PooledOrder)) // by id: masked names can repeat
            list.Add(new(null, "pooledOrder", string.Join(" ", recorded.PooledOrder.Select(name)), string.Join(" ", replayed.PooledOrder.Select(name))));
        if (list.Count == 0)
            list.Add(new(null, "order", null, null)); // same outcomes, listed in another draw order
        return list;
    }

    private static void Add(List<RunMismatch> list, string? vendor, string field, string? recorded, string? replayed)
    {
        if (recorded != replayed) list.Add(new(vendor, field, recorded, replayed));
    }

    private static string? Text(int? n) => n?.ToString(CultureInfo.InvariantCulture);

    private static string Quota(QuotaLine q) => string.Create(CultureInfo.InvariantCulture, $"{q.Category}:{q.Allocated}");
}
