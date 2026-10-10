using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.StallRequests;

public sealed record SubmitStallRequest(Guid RoundId);

/// <summary>
/// Vendor submits a request for an open round. Rules: the round must be accepting requests (by <see cref="IClock"/>),
/// and the vendor may not hold another unfinished request in the district for an overlapping round period.
/// </summary>
public sealed class SubmitStallRequestHandler(IAppDbContext db, IClock clock)
{
    public const string Action = "StallRequest.Submit";

    public async Task<Result<StallRequestDto>> Handle(Guid vendorId, SubmitStallRequest command, CancellationToken ct)
    {
        var now = clock.Now;
        var vendor = await db.Vendors.SingleOrDefaultAsync(v => v.Id == vendorId, ct);
        if (vendor is null)
            return Error.Forbidden("vendor.unknown", "ไม่พบข้อมูลผู้ค้า");

        var target = await (
            from r in db.Rounds
            join s in db.Spaces on r.SpaceId equals s.Id
            where r.Id == command.RoundId
            select new { Round = r, Space = s }).SingleOrDefaultAsync(ct);
        if (target is null)
            return Error.NotFound("round.not_found", "ไม่พบรอบที่ต้องการยื่นคำขอ");
        if (!target.Round.AcceptsRequestsAt(now))
            return Error.Conflict("round.closed", "รอบนี้ไม่ได้เปิดรับคำขอในขณะนี้");

        var blocking = await (
            from req in db.StallRequests
            join r in db.Rounds on req.RoundId equals r.Id
            join s in db.Spaces on r.SpaceId equals s.Id
            where req.VendorId == vendorId
                  && s.DistrictId == target.Space.DistrictId
                  && !StallRequest.Finished.Contains(req.Status)
                  && r.StartsOn <= target.Round.EndsOn && target.Round.StartsOn <= r.EndsOn
            select s.Name).FirstOrDefaultAsync(ct);
        if (blocking is not null)
            return Error.Conflict("request.already_open",
                $"มีคำขอที่ยังไม่สิ้นสุดอยู่แล้วที่ {blocking} — ยื่นได้ 1 คำขอต่อช่วงรอบในเขต");

        var previousRoundId = await db.Rounds
            .Where(r => r.SpaceId == target.Space.Id && r.EndsOn < target.Round.StartsOn)
            .OrderByDescending(r => r.EndsOn)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
        var heldLockHere = previousRoundId is not null && await db.StallRequests.AnyAsync(
            req => req.RoundId == previousRoundId && req.VendorId == vendorId && StallRequest.HeldLock.Contains(req.Status), ct);

        var request = new StallRequest(
            Guid.CreateVersion7(now), target.Round.Id, vendorId, now,
            StallRequest.CategoryFor(vendor.VulnerableReason is not null, heldLockHere));
        db.StallRequests.Add(request);
        await db.CommitAsync(Action, ct);

        return new StallRequestDto(
            request.Id, target.Round.Id, target.Space.Id, target.Space.Name, target.Round.StartsOn, target.Round.EndsOn,
            request.SubmittedAt, request.Category, request.Status, request.IneligibleReason);
    }
}
