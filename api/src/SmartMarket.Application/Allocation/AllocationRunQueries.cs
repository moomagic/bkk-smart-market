using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;

namespace SmartMarket.Application.Allocation;

public sealed class GetAllocationRunHandler(IAppDbContext db)
{
    public async Task<Result<AllocationRunDto>> Handle(Guid runId, CancellationToken ct) =>
        (await AllocationRunQueries.Dtos(db, db.AllocationRuns.Where(r => r.Id == runId), ct)).SingleOrDefault() is { } run
            ? run
            : Error.NotFound("allocation_run.not_found", "ไม่พบผลการจัดสรรนี้");
}

/// <summary>Every run of a round, newest first — rejected ones stay visible (ADR-0002).</summary>
public sealed class ListRoundAllocationRunsHandler(IAppDbContext db)
{
    public async Task<Result<IReadOnlyList<AllocationRunDto>>> Handle(Guid roundId, CancellationToken ct)
    {
        if (!await db.Rounds.AnyAsync(r => r.Id == roundId, ct))
            return Error.NotFound("round.not_found", "ไม่พบรอบนี้");
        return await AllocationRunQueries.Dtos(
            db, db.AllocationRuns.Where(r => r.RoundId == roundId).OrderByDescending(r => r.RanAt), ct);
    }
}

internal static class AllocationRunQueries
{
    /// <summary>Unpacks the stored result and joins masked names for display.</summary>
    public static async Task<List<AllocationRunDto>> Dtos(IAppDbContext db, IQueryable<AllocationRun> runs, CancellationToken ct)
    {
        var rows = await (
            from run in runs.AsNoTracking()
            join r in db.Rounds on run.RoundId equals r.Id
            join s in db.Spaces on r.SpaceId equals s.Id
            select new { Run = run, SpaceName = s.Name }).ToListAsync(ct);
        if (rows.Count == 0) return [];

        var roundIds = rows.Select(x => x.Run.RoundId).Distinct().ToList();
        var drawn = await (
            from q in db.StallRequests
            join v in db.Vendors on q.VendorId equals v.Id
            where roundIds.Contains(q.RoundId)
            select new { q.RoundId, q.VendorId, v.MaskedName }).ToListAsync(ct);
        var byRound = drawn.ToLookup(x => x.RoundId);

        return [.. rows.Select(x =>
        {
            var result = AllocationJson.Deserialize<AllocationResult>(x.Run.ResultJson);
            var vendors = byRound[x.Run.RoundId].ToDictionary(v => v.VendorId);
            return new AllocationRunDto(
                x.Run.Id, x.Run.RoundId, x.SpaceName, x.Run.ReferenceNo, x.Run.Seed, x.Run.InputHash, x.Run.Status,
                x.Run.RanAt, x.Run.RanBy, x.Run.DecidedAt, x.Run.DecidedBy, AllocationJson.Deserialize<RulesSnapshot>(x.Run.RulesSnapshot), result.PooledDraw,
                result.Quota,
                [.. result.Outcomes.Select(o => new RunOutcomeRow(
                    o.VendorId, vendors[o.VendorId].MaskedName, o.Category, o.Weight, o.Kind,
                    o.LockCode, o.WaitlistPosition, o.ViaRollover, o.Reason))],
                result.UnassignedLockCodes);
        })];
    }
}
