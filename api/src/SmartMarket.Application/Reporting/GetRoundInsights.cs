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
            select new { r.StartsOn, r.EndsOn, s.OpenHours, s.DistrictId }).SingleOrDefaultAsync(ct);
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
                .Select(c => new { c.AssignmentId, c.Day, c.WithinGeofence, c.DeviceHash }).ToListAsync(ct))
            .ToLookup(c => c.AssignmentId, c => new InsightCheckIn(c.Day, c.WithinGeofence, c.DeviceHash));

        var usage = rules.Value.Usage;
        var today = CheckIn.DayOf(clock.Now);
        // Any winner in the district, any round: a device shared across spaces is the case a per-round check misses
        var windowFrom = today.AddDays(1 - usage.SharedDeviceWindowDays);
        var district = await (
            from c in db.CheckIns.AsNoTracking()
            join a in db.Assignments on c.AssignmentId equals a.Id
            join v in db.Vendors on a.VendorId equals v.Id
            join l in db.Locks on a.LockId equals l.Id
            join s in db.Spaces on l.SpaceId equals s.Id
            where s.DistrictId == round.DistrictId && c.DeviceHash != null && c.Day >= windowFrom && c.Day <= today
            select new DeviceCheckIn(a.VendorId, v.MaskedName, s.Name, l.Code, c.Day, c.DeviceHash!)).ToListAsync(ct);

        var input = new InsightRound(round.StartsOn, round.EndsOn, SellingDays.Parse(round.OpenHours),
            [.. holders.Select(h => new InsightHolder(h.Id, h.MaskedName, Vendor.SurnameOf(h.DisplayName), h.Phone, h.Code, [.. checkIns[h.AssignmentId]]))],
            district);
        return InsightRules.Evaluate(input, today,
            new UsageThresholds(usage.MinCheckInRatio, usage.AbsentDaysWarning, usage.AbsentDaysRevoke,
                usage.SharedDeviceMinDays, usage.SharedDeviceWindowDays)).ToList();
    }
}
