using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Common;
using SmartMarket.Application.Rounds;
using SmartMarket.Application.StallRequests;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Reviews;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Reviews;

public sealed record RespondReview(ReviewOutcome? Outcome, string? Response);

/// <summary>
/// The district answers a review once: Upheld, or Corrected — only for an eligibility mistake, so only on an
/// Ineligible request. Corrected before approval rejects the draft (if any) without counting toward the reject limit
/// and lets the request into the redraw; after approval it gets a vacant lock or the end of its group's waitlist and
/// the winners stay as they are (ADR-0005) (CLAUDE.md › Business rules). The weight is recomputed by the carry-over rule, never typed
/// in, so a correction cannot shape the draw (ADR-0002). One command, one audit entry.
/// </summary>
public sealed class RespondReviewHandler(IAppDbContext db, IClock clock, ICurrentActor actor, IOptions<RulesOptions> rules)
{
    public const string Action = "Round.RespondReview";
    public const string CorrectedRejectReason = "แก้ไขผลตรวจสิทธิจากคำขอทบทวน";
    public const int MaxResponseLength = 1000;

    public async Task<Result<ReviewDto>> Handle(Guid reviewId, RespondReview command, CancellationToken ct)
    {
        var response = command.Response?.Trim();
        if (command.Outcome is not { } outcome)
            return Error.Invalid("review.outcome_required", "กรุณาเลือกผลการทบทวน");
        if (string.IsNullOrEmpty(response))
            return Error.Invalid("review.response_required", "กรุณาเขียนคำตอบถึงผู้ค้า");
        if (response.Length > MaxResponseLength)
            return Error.Invalid("review.response_too_long", $"คำตอบยาวเกิน {MaxResponseLength} ตัวอักษร");

        var vendorId = await (
            from r in db.Reviews.AsNoTracking()
            join q in db.StallRequests on r.StallRequestId equals q.Id
            where r.Id == reviewId
            select (Guid?)q.VendorId).SingleOrDefaultAsync(ct);
        if (vendorId is null)
            return Error.NotFound("review.not_found", "ไม่พบคำขอทบทวนนี้");

        // A correction re-opens a finished request, so it checks ADR-0003 like a submit does — one at a time per vendor.
        return await db.SerializedForVendorAsync(vendorId.Value, () => Answer(reviewId, outcome, response, ct), ct);
    }

    private async Task<Result<ReviewDto>> Answer(Guid reviewId, ReviewOutcome outcome, string response, CancellationToken ct)
    {
        var review = await db.Reviews.SingleAsync(r => r.Id == reviewId, ct);
        if (!review.IsOpen)
            return Error.Conflict("review.answered", "คำขอทบทวนนี้ตอบไปแล้ว");
        var request = await db.StallRequests.SingleAsync(q => q.Id == review.StallRequestId, ct);
        var round = await db.Rounds.SingleAsync(r => r.Id == request.RoundId, ct);

        var now = clock.Now;
        if (outcome == ReviewOutcome.Corrected)
        {
            if (request.Status != StallRequestStatus.Ineligible)
                return Error.Conflict("review.not_correctable",
                    "แก้ไขได้เฉพาะผลตรวจสิทธิที่ไม่ผ่าน ผลการจับฉลากทบทวนไม่ได้ ตอบได้เพียง \"ยืนตามเดิม\"");
            var correction = await Correct(request, round, now, ct);
            if (!correction.IsSuccess) return correction.Error!;
        }

        review.Answer(outcome, response, now, actor.Actor);
        await db.CommitAsync(Action, AuditSubject.For<Round>(round.Id), ct);
        return ReviewDto.From(review);
    }

    private async Task<Result<bool>> Correct(StallRequest request, Round round, DateTimeOffset now, CancellationToken ct)
    {
        // ADR-0003: while Ineligible the request was finished, so the vendor may have asked elsewhere meanwhile.
        var space = await db.Spaces.AsNoTracking().SingleAsync(s => s.Id == round.SpaceId, ct);
        var elsewhere = await (
            from q in db.StallRequests
            join r in db.Rounds.Where(Round.Overlapping(round.StartsOn, round.EndsOn)) on q.RoundId equals r.Id
            join s in db.Spaces on r.SpaceId equals s.Id
            where q.VendorId == request.VendorId && q.Id != request.Id && s.DistrictId == space.DistrictId
                  && !StallRequest.Finished.Contains(q.Status)
            select s.Name).FirstOrDefaultAsync(ct);
        if (elsewhere is not null)
            return Error.Conflict("review.vendor_has_other_request",
                $"ผู้ค้ามีคำขอที่ยังไม่สิ้นสุดที่ {elsewhere} ในช่วงรอบเดียวกัน จึงแก้ไขคำขอนี้ไม่ได้");

        var previousRoundId = await RoundQueries.PreviousRoundId(db, round.SpaceId, round.StartsOn, ct);
        var previous = previousRoundId is null ? null : await db.StallRequests
            .Where(q => q.RoundId == previousRoundId && q.VendorId == request.VendorId)
            .Select(q => (StallRequestStatus?)q.Status).FirstOrDefaultAsync(ct);
        var weight = EligibilityChecker.Weight(previous, rules.Value.CarryOverWeight);

        var approved = await db.AllocationRuns.SingleOrDefaultAsync(
            r => r.RoundId == round.Id && r.Status == AllocationRunStatus.Approved, ct);
        if (approved is null)
        {
            var draft = await db.AllocationRuns.SingleOrDefaultAsync(r => r.RoundId == round.Id && r.Status == AllocationRunStatus.Draft, ct);
            if (draft is not null)
                await Rejection.Apply(db, draft, CorrectedRejectReason, now, actor.Actor, countsTowardLimit: false, ct);
            request.CorrectToEligible(weight);
            return true;
        }

        // After approval: a vacant lock if there is one, else the end of the group's waitlist as it stands now (ADR-0005).
        var (result, admitted) = AllocationEngine.AdmitCorrected(
            AllocationJson.Deserialize<AllocationResult>(approved.CurrentResultJson), request.VendorId, request.Category, weight);
        if (admitted.LockCode is { } code)
        {
            request.CorrectToWon(weight);
            var lockId = await db.Locks.Where(l => l.SpaceId == round.SpaceId && l.Code == code).Select(l => l.Id).SingleAsync(ct);
            db.Assignments.Add(new Assignment(Guid.CreateVersion7(now), round.Id, request.VendorId, lockId,
                now.AddDays(rules.Value.ConfirmWithinDays)));
        }
        else
        {
            request.CorrectToWaitlist(weight, admitted.WaitlistPosition!.Value);
        }
        approved.UpdateCurrentResult(AllocationJson.Serialize(result));
        return true;
    }
}
