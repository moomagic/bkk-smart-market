using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Allocation;

/// <summary>
/// A winner loses their lock — by choice or by missing the confirm deadline (CLAUDE.md › Allocation algorithm 9).
/// <see cref="AllocationEngine.Promote"/> picks who moves up; this applies it to requests and assignments and keeps
/// the run's current result in step. The run's <see cref="AllocationRun.ResultJson"/> (the draw record) is never touched.
/// Does not commit: the caller's command does, so forfeit and promotion land in one audit entry.
/// </summary>
internal static class Forfeiture
{
    public const string ExpiredReason = "ไม่ยืนยันสิทธิ์ภายในกำหนด";

    /// <param name="reason">Replaces the engine's generic reason on the forfeiter's outcome, if given.</param>
    public static async Task Apply(
        IAppDbContext db, Assignment assignment, DateTimeOffset now, int confirmWithinDays, string? reason, CancellationToken ct)
    {
        var run = await db.AllocationRuns.SingleAsync(
            r => r.RoundId == assignment.RoundId && r.Status == AllocationRunStatus.Approved, ct);
        var requests = await db.StallRequests.Where(q => q.RoundId == assignment.RoundId).ToDictionaryAsync(q => q.VendorId, ct);

        var promotion = AllocationEngine.Promote(AllocationJson.Deserialize<AllocationResult>(run.CurrentResultJson), assignment.VendorId);
        var result = reason is null ? promotion.Result : promotion.Result with
        {
            Outcomes = [.. promotion.Result.Outcomes.Select(o => o.VendorId == assignment.VendorId ? o with { Reason = reason } : o)],
        };

        assignment.Forfeit(now);
        requests[assignment.VendorId].MarkForfeited();
        if (promotion.PromotedVendorId is { } promoted)
        {
            requests[promoted].PromoteToWon();
            // A fresh deadline from now, not the forfeiter's — otherwise a clock jump would burn through the whole waitlist.
            db.Assignments.Add(new Assignment(
                Guid.CreateVersion7(now), assignment.RoundId, promoted, assignment.LockId, now.AddDays(confirmWithinDays)));
        }
        foreach (var o in result.Outcomes.Where(o => o.Kind == OutcomeKind.Waitlisted))
            if (requests[o.VendorId].WaitlistPosition != o.WaitlistPosition)
                requests[o.VendorId].MoveUpWaitlist(o.WaitlistPosition!.Value);

        run.UpdateCurrentResult(AllocationJson.Serialize(result));
    }
}
