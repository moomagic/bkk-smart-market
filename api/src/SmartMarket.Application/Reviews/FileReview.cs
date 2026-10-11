using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;
using SmartMarket.Application.StallRequests;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Reviews;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Reviews;

public sealed record FileReview(string? Reason);

/// <summary>
/// A vendor asks the district to re-check an Ineligible result — draw outcomes cannot be reviewed — within
/// <see cref="ReviewRules.FileWithinDays"/> of it (CLAUDE.md › ขอทบทวนผล). The result dates from the approval of the
/// round's run when there is one, otherwise from the eligibility check, so the window starts again when the round is
/// approved and results are announced. One review per request (ADR-0005).
/// </summary>
public sealed class FileReviewHandler(IAppDbContext db, IClock clock, ICurrentActor actor, IOptions<RulesOptions> rules)
{
    public const string Action = "Round.FileReview";
    public const int MaxReasonLength = 500;

    public async Task<Result<ReviewDto>> Handle(Guid requestId, FileReview command, CancellationToken ct)
    {
        if (await VendorLookup.Current(db, actor, ct) is not { } vendor)
            return VendorLookup.Unknown;
        var reason = command.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            return Error.Invalid("review.reason_required", "กรุณาระบุเหตุผลที่ขอทบทวน");
        if (reason.Length > MaxReasonLength)
            return Error.Invalid("review.reason_too_long", $"เหตุผลยาวเกิน {MaxReasonLength} ตัวอักษร");

        var request = await db.StallRequests.AsNoTracking().SingleOrDefaultAsync(q => q.Id == requestId && q.VendorId == vendor.Id, ct);
        if (request is null)
            return Error.NotFound("request.not_found", "ไม่พบคำขอนี้");

        if (request.Status != StallRequestStatus.Ineligible)
            return Error.Conflict("review.not_reviewable",
                "ขอทบทวนได้เฉพาะผลการตรวจสิทธิที่ไม่ผ่าน ผลการจับฉลากทบทวนไม่ได้");
        var approvedAt = await db.AllocationRuns
            .Where(r => r.RoundId == request.RoundId && r.Status == AllocationRunStatus.Approved)
            .Select(r => r.DecidedAt).FirstOrDefaultAsync(ct);
        var resultAt = approvedAt ?? request.EligibilityCheckedAt!.Value;

        var now = clock.Now;
        var days = rules.Value.Review.FileWithinDays;
        if (now > resultAt.AddDays(days))
            return Error.Conflict("review.too_late", $"ขอทบทวนได้ภายใน {days} วันหลังประกาศผล ซึ่งเลยกำหนดแล้ว");
        if (await db.Reviews.AnyAsync(r => r.StallRequestId == request.Id, ct))
            return Error.Conflict("review.already_filed", "ยื่นขอทบทวนคำขอนี้ไปแล้ว (ทบทวนได้ครั้งเดียว)");

        var review = new Review(Guid.CreateVersion7(now), request.Id, reason, now);
        db.Reviews.Add(review);
        await db.CommitAsync(Action, AuditSubject.For<Round>(request.RoundId), ct);
        return ReviewDto.From(review);
    }
}
