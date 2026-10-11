using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Common;
using SmartMarket.Application.Reviews;
using SmartMarket.Domain.Allocation;

namespace SmartMarket.Application.StallRequests;

public sealed class ListMyStallRequestsHandler(IAppDbContext db, ICurrentActor actor, AssignmentExpiry expiry)
{
    public async Task<Result<IReadOnlyList<StallRequestDto>>> Handle(CancellationToken ct)
    {
        if (await VendorLookup.Current(db, actor, ct) is not { } vendor)
            return VendorLookup.Unknown;
        var roundIds = await db.StallRequests.Where(q => q.VendorId == vendor.Id).Select(q => q.RoundId).ToListAsync(ct);
        await expiry.ForfeitExpired(roundIds, ct);
        return await MyStallRequests.For(db, vendor.Id, ct);
    }
}

internal static class MyStallRequests
{
    /// <summary>The vendor's requests, newest round first, with outcomes from approved runs only.</summary>
    public static async Task<List<StallRequestDto>> For(IAppDbContext db, Guid vendorId, CancellationToken ct)
    {
        var requests = await (
            from req in db.StallRequests.AsNoTracking()
            join r in db.Rounds on req.RoundId equals r.Id
            join s in db.Spaces on r.SpaceId equals s.Id
            where req.VendorId == vendorId
            orderby r.StartsOn descending
            select new { Request = req, Round = r, SpaceName = s.Name }).ToListAsync(ct);

        var roundIds = requests.Select(x => x.Round.Id).ToList();
        var runs = (await db.AllocationRuns.AsNoTracking()
                .Where(r => roundIds.Contains(r.RoundId))
                .OrderByDescending(r => r.RanAt)
                .Select(r => new { r.RoundId, r.ReferenceNo, r.Status, r.RanAt, r.RejectReason })
                .ToListAsync(ct))
            .ToLookup(r => r.RoundId);
        // Outcomes come from the approved run only, so a draft never leaks to vendors; the current result, so a
        // promoted vendor reads why they moved up.
        var approvedResults = await db.AllocationRuns.AsNoTracking()
            .Where(r => roundIds.Contains(r.RoundId) && r.Status == AllocationRunStatus.Approved)
            .ToDictionaryAsync(r => r.RoundId, r => r.CurrentResultJson, ct);
        var assignments = await (
            from a in db.Assignments.AsNoTracking().Where(Assignment.Active)
            join l in db.Locks on a.LockId equals l.Id
            where a.VendorId == vendorId && roundIds.Contains(a.RoundId)
            select new
            {
                a.RoundId, l.Code, a.ConfirmBy,
                LastCheckInAt = db.CheckIns.Where(c => c.AssignmentId == a.Id).Max(c => (DateTimeOffset?)c.At),
            }).ToDictionaryAsync(a => a.RoundId, ct);

        var requestIds = requests.Select(x => x.Request.Id).ToList();
        var reviews = (await db.Reviews.AsNoTracking().Where(r => requestIds.Contains(r.StallRequestId)).ToListAsync(ct))
            .GroupBy(r => r.StallRequestId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.SubmittedAt).First());

        return [.. requests.Select(x =>
        {
            var (req, round) = (x.Request, x.Round);
            var approved = runs[round.Id].FirstOrDefault(r => r.Status == AllocationRunStatus.Approved);
            var outcome = approvedResults.TryGetValue(round.Id, out var json)
                ? AllocationJson.Deserialize<AllocationResult>(json).Outcomes.SingleOrDefault(o => o.VendorId == vendorId)
                : null;
            var assignment = assignments.GetValueOrDefault(round.Id);
            return new StallRequestDto(
                req.Id, round.Id, round.SpaceId, x.SpaceName, round.StartsOn, round.EndsOn, req.SubmittedAt, req.Category,
                req.Status, req.IneligibleReason, req.WaitlistPosition, assignment?.Code, outcome?.Reason,
                approved?.ReferenceNo, assignment?.ConfirmBy,
                [.. runs[round.Id].Select(r => new RunReference(r.ReferenceNo, r.Status, r.RanAt, r.RejectReason))],
                assignment?.LastCheckInAt,
                reviews.TryGetValue(req.Id, out var review) ? ReviewDto.From(review) : null);
        })];
    }
}
