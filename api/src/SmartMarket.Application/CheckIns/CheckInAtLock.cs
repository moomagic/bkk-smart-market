using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Common;
using SmartMarket.Application.StallRequests;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.CheckIns;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.CheckIns;

/// <param name="WithinGeofence">Simulated location result from the phone.</param>
/// <param name="DeviceId">Simulated device identity (a random id the web app keeps); stored only as a hash.</param>
public sealed record CheckInAtLock(Guid StallRequestId, string QrCode, bool WithinGeofence, string? DeviceId = null);

public sealed record CheckInDto(Guid Id, Guid StallRequestId, string SpaceName, string LockCode, DateTimeOffset At, bool WithinGeofence);

/// <summary>
/// A confirmed winner checks in at their lock by scanning its QR (simulated): once per Bangkok day, until the round
/// ends. Allowed from confirmation on, so the demo can check in straight after confirming.
/// </summary>
public sealed class CheckInAtLockHandler(IAppDbContext db, IClock clock, ICurrentActor actor, AssignmentExpiry expiry)
{
    public const string Action = "Round.CheckIn";
    public const int MaxDeviceIdLength = 100;

    public async Task<Result<CheckInDto>> Handle(CheckInAtLock command, CancellationToken ct)
    {
        if (command.DeviceId is { Length: > MaxDeviceIdLength })
            return Error.Invalid("checkin.device_id_too_long", $"รหัสอุปกรณ์ยาวเกิน {MaxDeviceIdLength} ตัวอักษร");
        if (await VendorLookup.Current(db, actor, ct) is not { } vendor)
            return VendorLookup.Unknown;
        var request = await db.StallRequests
            .SingleOrDefaultAsync(q => q.Id == command.StallRequestId && q.VendorId == vendor.Id, ct);
        if (request is null)
            return Error.NotFound("request.not_found", "ไม่พบคำขอนี้");
        await expiry.ForfeitExpired([request.RoundId], ct); // a missed deadline reads as forfeited, not "confirm first"
        if (request.Status == StallRequestStatus.Won)
            return Error.Conflict("checkin.not_confirmed", "กรุณายืนยันสิทธิ์ก่อนเช็คอิน");
        if (request.Status != StallRequestStatus.Confirmed)
            return Error.Conflict("checkin.not_holder", "คำขอนี้ไม่ได้ถือสิทธิ์ล็อค จึงเช็คอินไม่ได้");

        var target = await (
            from a in db.Assignments.Where(Assignment.Active)
            join l in db.Locks on a.LockId equals l.Id
            join r in db.Rounds on a.RoundId equals r.Id
            join s in db.Spaces on r.SpaceId equals s.Id
            where a.RoundId == request.RoundId && a.VendorId == vendor.Id
            select new { Assignment = a, Lock = l, Round = r, SpaceName = s.Name }).SingleAsync(ct);

        var now = clock.Now;
        var today = CheckIn.DayOf(now);
        if (today > target.Round.EndsOn)
            return Error.Conflict("checkin.round_ended", "รอบนี้สิ้นสุดแล้ว เช็คอินไม่ได้");
        if (command.QrCode != CheckInQr.For(target.Round.SpaceId, target.Lock.Code))
            return Error.Conflict("checkin.wrong_qr", $"QR ไม่ตรงกับล็อค {target.Lock.Code} ของคุณ");
        if (await db.CheckIns.AnyAsync(c => c.AssignmentId == target.Assignment.Id && c.Day == today, ct))
            return Error.Conflict("checkin.already_today", "เช็คอินวันนี้แล้ว");

        var checkIn = new CheckIn(
            Guid.CreateVersion7(now), target.Assignment.Id, now, CheckInMethod.QR, command.WithinGeofence, DeviceHash.Of(command.DeviceId));
        db.CheckIns.Add(checkIn);
        await db.CommitAsync(Action, AuditSubject.For<Round>(request.RoundId), ct);

        return new CheckInDto(checkIn.Id, request.Id, target.SpaceName, target.Lock.Code, checkIn.At, checkIn.WithinGeofence);
    }
}
