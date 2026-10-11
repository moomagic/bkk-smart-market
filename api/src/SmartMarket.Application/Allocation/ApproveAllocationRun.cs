using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Allocation;

/// <summary>
/// Officer approves a draft run (CLAUDE.md › Allocation algorithm 8): every drawn request receives its outcome,
/// each winner gets an Assignment to confirm within <see cref="RulesOptions.ConfirmWithinDays"/>, and the round
/// becomes Approved. One audit entry, filed under the Round.
/// </summary>
public sealed class ApproveAllocationRunHandler(IAppDbContext db, IClock clock, ICurrentActor actor, IOptions<RulesOptions> rules)
{
    public const string Action = "Round.ApproveAllocation";

    public async Task<Result<AllocationRunDto>> Handle(Guid runId, CancellationToken ct)
    {
        var run = await db.AllocationRuns.SingleOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
            return Error.NotFound("allocation_run.not_found", "ไม่พบผลการจัดสรรนี้");
        if (run.Status != AllocationRunStatus.Draft)
            return Error.Conflict("allocation_run.not_draft", "อนุมัติได้เฉพาะผลจัดสรรที่เป็นร่าง");
        var round = await db.Rounds.SingleAsync(r => r.Id == run.RoundId, ct);

        var result = AllocationJson.Deserialize<AllocationResult>(run.ResultJson);
        var requests = await db.StallRequests
            .Where(q => q.RoundId == round.Id && q.Status == StallRequestStatus.Eligible)
            .ToDictionaryAsync(q => q.VendorId, ct);
        var locks = await db.Locks.Where(l => l.SpaceId == round.SpaceId).ToDictionaryAsync(l => l.Code, l => l.Id, ct);
        // The round is frozen once drawn, so the draft and the requests must still line up; anything else is a bug.
        if (requests.Count != result.Outcomes.Count || result.Outcomes.Any(o => !requests.ContainsKey(o.VendorId)))
            throw new InvalidOperationException($"Run {run.ReferenceNo} no longer matches the eligible requests of its round.");

        var now = clock.Now;
        var confirmBy = now.AddDays(rules.Value.ConfirmWithinDays);
        foreach (var o in result.Outcomes)
        {
            var request = requests[o.VendorId];
            switch (o.Kind)
            {
                case OutcomeKind.Won:
                    request.MarkWon();
                    db.Assignments.Add(new Assignment(Guid.CreateVersion7(now), round.Id, o.VendorId, locks[o.LockCode!], confirmBy));
                    break;
                case OutcomeKind.Waitlisted:
                    request.MarkWaitlisted(o.WaitlistPosition!.Value);
                    break;
                case OutcomeKind.NotSelected:
                    request.MarkNotSelected();
                    break;
                default:
                    throw new InvalidOperationException($"A fresh draw has no {o.Kind} outcome.");
            }
        }
        run.Approve(now, actor.Actor);
        round.MarkApproved();
        await db.CommitAsync(Action, AuditSubject.For<Round>(round.Id), ct);

        return (await AllocationRunQueries.Dtos(db, db.AllocationRuns.Where(r => r.Id == run.Id), ct)).Single();
    }
}
