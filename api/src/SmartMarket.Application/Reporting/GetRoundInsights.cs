using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.CheckIns;
using SmartMarket.Domain.Vendors;

namespace SmartMarket.Application.Reporting;

/// <summary>
/// Allocation Insight for one round, read-only (see <see cref="InsightRules"/>): the flags are for an officer to look
/// into and change nothing — no forfeit, no revoke, no audit entry (Guardrail 1). The round's holders are the vendors
/// with a confirmed, still-active assignment.
/// </summary>
public sealed class GetRoundInsightsHandler(IAppDbContext db, IClock clock, IOptions<RulesOptions> rules)
{
    public async Task<Result<IReadOnlyList<InsightFlag>>> Handle(Guid roundId, CancellationToken ct)
    {
        var round = await (
            from r in db.Rounds.AsNoTracking()
            join s in db.Spaces on r.SpaceId equals s.Id
            where r.Id == roundId
            select new { r.StartsOn, r.EndsOn, s.OpenHours }).SingleOrDefaultAsync(ct);
        if (round is null)
            return Error.NotFound("round.not_found", "ไม่พบรอบนี้");

        var holders = await (
            from a in db.Assignments.AsNoTracking().Where(Assignment.Active)
            join v in db.Vendors on a.VendorId equals v.Id
            join l in db.Locks on a.LockId equals l.Id
            where a.RoundId == roundId && a.ConfirmedAt != null
            select new { AssignmentId = a.Id, v.Id, v.MaskedName, v.DisplayName, v.Phone, l.Code }).ToListAsync(ct);
        var assignmentIds = holders.Select(h => h.AssignmentId).ToList();
        var checkIns = (await db.CheckIns.AsNoTracking().Where(c => assignmentIds.Contains(c.AssignmentId))
                .Select(c => new { c.AssignmentId, c.Day, c.WithinGeofence }).ToListAsync(ct))
            .ToLookup(c => c.AssignmentId, c => new InsightCheckIn(c.Day, c.WithinGeofence));

        var usage = rules.Value.Usage;
        var input = new InsightRound(round.StartsOn, round.EndsOn, SellingDays.Parse(round.OpenHours),
            [.. holders.Select(h => new InsightHolder(h.Id, h.MaskedName, Vendor.SurnameOf(h.DisplayName), h.Phone, h.Code, [.. checkIns[h.AssignmentId]]))]);
        return InsightRules.Evaluate(input, CheckIn.DayOf(clock.Now),
            new UsageThresholds(usage.MinCheckInRatio, usage.AbsentDaysWarning, usage.AbsentDaysRevoke)).ToList();
    }
}
