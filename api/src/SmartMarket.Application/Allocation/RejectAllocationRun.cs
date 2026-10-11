using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Rounds;

namespace SmartMarket.Application.Allocation;

public sealed record RejectAllocationRun(string? Reason);

/// <summary>
/// Officer rejects a draft with a reason (ADR-0002). The round goes back to Closed so eligibility can be fixed and it
/// can be drawn again with a fresh seed. A round allows <see cref="RejectRules.MaxPerRoundWithoutExecutive"/> counted
/// rejects; past that only an executive may reject, so nobody can keep re-drawing for a result they like.
/// The rejected run stays visible to vendors with its reference number and reason.
/// </summary>
public sealed class RejectAllocationRunHandler(IAppDbContext db, IClock clock, ICurrentActor actor, IOptions<RulesOptions> rules)
{
    public const string Action = "Round.RejectAllocation";
    public const string ExecutiveRole = "executive";
    public const int MaxReasonLength = 500;

    public async Task<Result<AllocationRunDto>> Handle(Guid runId, RejectAllocationRun command, CancellationToken ct)
    {
        var reason = command.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            return Error.Invalid("reject.reason_required", "กรุณาระบุเหตุผลในการปฏิเสธผลจัดสรร");
        if (reason.Length > MaxReasonLength)
            return Error.Invalid("reject.reason_too_long", $"เหตุผลยาวเกิน {MaxReasonLength} ตัวอักษร");

        var run = await db.AllocationRuns.SingleOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
            return Error.NotFound("allocation_run.not_found", "ไม่พบผลการจัดสรรนี้");
        if (run.Status != AllocationRunStatus.Draft)
            return Error.Conflict("allocation_run.not_draft", "ปฏิเสธได้เฉพาะผลจัดสรรที่เป็นร่าง");

        if (actor.Role != ExecutiveRole)
        {
            var max = rules.Value.Reject.MaxPerRoundWithoutExecutive;
            var counted = await db.AllocationRuns.Where(AllocationRun.CountedReject).CountAsync(r => r.RoundId == run.RoundId, ct);
            if (counted >= max)
                return Error.Forbidden("reject.limit_reached",
                    $"รอบนี้ปฏิเสธผลครบ {max} ครั้งแล้ว ครั้งต่อไปต้องให้ผู้บริหารเป็นผู้ปฏิเสธ");
        }

        await Rejection.Apply(db, run, reason, clock.Now, actor.Actor, countsTowardLimit: true, ct);
        await db.CommitAsync(Action, AuditSubject.For<Round>(run.RoundId), ct);

        return (await AllocationRunQueries.Dtos(db, db.AllocationRuns.Where(r => r.Id == run.Id), ct)).Single();
    }
}

/// <summary>
/// Rejecting a draft, without committing — the officer's reject and a Corrected review's reject each commit it as
/// their own single command.
/// </summary>
internal static class Rejection
{
    /// <param name="countsTowardLimit">False only for a reject caused by a Corrected review (ADR-0002).</param>
    public static async Task Apply(
        IAppDbContext db, AllocationRun run, string reason, DateTimeOffset at, string by, bool countsTowardLimit, CancellationToken ct)
    {
        var round = await db.Rounds.SingleAsync(r => r.Id == run.RoundId, ct);
        run.Reject(at, by, reason, countsTowardLimit);
        round.ReopenForRedraw();
    }
}
