using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.StallRequests;

/// <summary>A winner takes the lock before the deadline. A missed deadline is forfeited first, so a late confirm fails.</summary>
public sealed class ConfirmStallRequestHandler(IAppDbContext db, IClock clock, ICurrentActor actor, AssignmentExpiry expiry)
{
    public const string Action = "Round.ConfirmAssignment";

    public async Task<Result<StallRequestDto>> Handle(Guid requestId, CancellationToken ct)
    {
        var target = await WinnerAction.LoadUnconfirmedWin(db, actor, expiry, requestId, ct);
        if (!target.IsSuccess) return target.Error!;
        var (request, assignment) = target.Value;

        assignment.Confirm(clock.Now);
        request.MarkConfirmed();
        await db.CommitAsync(Action, AuditSubject.For<Round>(request.RoundId), ct);
        return (await MyStallRequests.For(db, request.VendorId, ct)).Single(r => r.Id == request.Id);
    }
}

/// <summary>A winner gives the lock up before confirming; the waitlist moves up in the same command.</summary>
public sealed class ForfeitStallRequestHandler(
    IAppDbContext db, IClock clock, ICurrentActor actor, AssignmentExpiry expiry, IOptions<RulesOptions> rules)
{
    public const string Action = "Round.ForfeitAssignment";

    public async Task<Result<StallRequestDto>> Handle(Guid requestId, CancellationToken ct)
    {
        var target = await WinnerAction.LoadUnconfirmedWin(db, actor, expiry, requestId, ct);
        if (!target.IsSuccess) return target.Error!;
        var (request, assignment) = target.Value;

        await Forfeiture.Apply(db, assignment, clock.Now, rules.Value.ConfirmWithinDays, reason: null, ct);
        await db.CommitAsync(Action, AuditSubject.For<Round>(request.RoundId), ct);
        return (await MyStallRequests.For(db, request.VendorId, ct)).Single(r => r.Id == request.Id);
    }
}

internal static class WinnerAction
{
    /// <summary>The vendor's own request, still Won and unconfirmed after missed deadlines are settled.</summary>
    public static async Task<Result<(StallRequest, Assignment)>> LoadUnconfirmedWin(
        IAppDbContext db, ICurrentActor actor, AssignmentExpiry expiry, Guid requestId, CancellationToken ct)
    {
        if (await VendorLookup.Current(db, actor, ct) is not { } vendor)
            return VendorLookup.Unknown;
        var request = await db.StallRequests.SingleOrDefaultAsync(q => q.Id == requestId && q.VendorId == vendor.Id, ct);
        if (request is null)
            return Error.NotFound("request.not_found", "ไม่พบคำขอนี้");

        await expiry.ForfeitExpired([request.RoundId], ct);
        switch (request.Status)
        {
            case StallRequestStatus.Confirmed:
                return Error.Conflict("request.already_confirmed",
                    "ยืนยันสิทธิ์แล้ว หากต้องการสละสิทธิ์กรุณาติดต่อสำนักงานเขตบางพลัด");
            case StallRequestStatus.Forfeited:
                return Error.Conflict("request.forfeited", "สิทธิ์นี้สิ้นสุดแล้ว (สละสิทธิ์หรือไม่ยืนยันภายในกำหนด)");
            case not StallRequestStatus.Won:
                return Error.Conflict("request.not_won", "คำขอนี้ไม่ได้รับสิทธิ์ จึงยืนยันหรือสละสิทธิ์ไม่ได้");
        }

        var assignment = await db.Assignments.Where(Assignment.Active)
            .SingleAsync(a => a.RoundId == request.RoundId && a.VendorId == vendor.Id, ct);
        return (request, assignment);
    }
}
