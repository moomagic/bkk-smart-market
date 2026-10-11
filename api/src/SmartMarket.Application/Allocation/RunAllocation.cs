using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Allocation;

/// <summary>
/// Officer draws a round into a Draft run (CLAUDE.md › Allocation algorithm 6–7). The seed comes from
/// <see cref="ISeedSource"/> — the command carries none. Requests keep their status: outcomes reach them only
/// on approval. The round moves to Drawn, which closes it to new requests and freezes eligibility.
/// </summary>
public sealed class RunAllocationHandler(
    IAppDbContext db, IClock clock, ICurrentActor actor, ISeedSource seeds, IOptions<RulesOptions> rules)
{
    public const string Action = "Round.RunAllocation";

    /// <summary>Pilot district code for reference numbers (เขตบางพลัด is the only district).</summary>
    private const string DistrictCode = "BPL";

    private static readonly TimeSpan Bangkok = TimeSpan.FromHours(7);

    public async Task<Result<AllocationRunDto>> Handle(Guid roundId, CancellationToken ct)
    {
        var round = await db.Rounds.SingleOrDefaultAsync(r => r.Id == roundId, ct);
        if (round is null)
            return Error.NotFound("round.not_found", "ไม่พบรอบนี้");
        var standing = await db.AllocationRuns
            .Where(r => r.RoundId == roundId && AllocationRun.Standing.Contains(r.Status))
            .Select(r => (AllocationRunStatus?)r.Status).FirstOrDefaultAsync(ct);
        if (standing == AllocationRunStatus.Draft)
            return Error.Conflict("round.has_draft", "รอบนี้มีร่างผลจัดสรรอยู่แล้ว ต้องปฏิเสธร่างเดิมก่อนจึงรันใหม่ได้");
        if (standing == AllocationRunStatus.Approved)
            return Error.Conflict("round.approved", "รอบนี้อนุมัติผลจัดสรรแล้ว รันใหม่ไม่ได้");
        if (!round.CanBeDrawn)
            return Error.Conflict("round.already_drawn", "รอบนี้จับฉลากแล้ว");

        var requests = await db.StallRequests.AsNoTracking()
            .Where(q => q.RoundId == roundId && StallRequest.AwaitingDraw.Contains(q.Status))
            .ToListAsync(ct);
        var uncheckedCount = requests.Count(q => q.Status == StallRequestStatus.Submitted);
        if (uncheckedCount > 0)
            return Error.Conflict("round.uncheckedCountrequests",
                $"มีคำขอที่ยังไม่ได้ตรวจสิทธิ {uncheckedCount} คำขอ กรุณาตรวจสิทธิทั้งรอบก่อนรันจัดสรร");
        if (!requests.Any(q => q.Status == StallRequestStatus.Eligible))
            return Error.Conflict("round.no_eligible_requests", "ไม่มีคำขอที่ผ่านการตรวจสิทธิ จึงรันจัดสรรไม่ได้");

        var lockCodes = await db.Locks.Where(l => l.SpaceId == round.SpaceId).Select(l => l.Code).ToListAsync(ct);
        var priorRuns = await db.AllocationRuns.CountAsync(r => r.RoundId == roundId, ct);
        var quota = rules.Value.Quota;
        var snapshot = new RulesSnapshot(quota.LegacyMax, quota.VulnerableMin, quota.NewMin, rules.Value.WaitlistPerGroup);
        var seed = seeds.NextSeed(roundId, priorRuns);

        var result = AllocationEngine.Allocate(new AllocationInput(
            lockCodes,
            [.. requests.Select(q => new Candidate(q.VendorId, q.Category, q.CarryOverWeight, q.Status == StallRequestStatus.Eligible))],
            seed, snapshot));

        var now = clock.Now;
        var run = new AllocationRun(
            Guid.CreateVersion7(now), roundId, await NextReferenceNo(now, ct), seed, result.InputHash,
            AllocationJson.Serialize(snapshot), now, actor.Actor, AllocationJson.Serialize(result));
        db.AllocationRuns.Add(run);
        round.MarkDrawn();
        await db.CommitAsync(Action, AuditSubject.For<Round>(roundId), ct);

        return (await AllocationRunQueries.Dtos(db, db.AllocationRuns.Where(r => r.Id == run.Id), ct)).Single();
    }

    /// <summary>BPL-{Buddhist year}-{MMdd}-{seq}: the Bangkok date of the run, numbered across the district that day.</summary>
    private async Task<string> NextReferenceNo(DateTimeOffset now, CancellationToken ct)
    {
        var local = now.ToOffset(Bangkok);
        var prefix = string.Create(CultureInfo.InvariantCulture, $"{DistrictCode}-{local.Year + 543}-{local:MMdd}-");
        var sameDay = await db.AllocationRuns.CountAsync(r => r.ReferenceNo.StartsWith(prefix), ct);
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}{sameDay + 1:00}");
    }
}
