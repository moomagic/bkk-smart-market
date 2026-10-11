using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Reporting;

/// <param name="Percent">Count ÷ Of × 100, one decimal; null when there is nothing to divide by.</param>
public sealed record Ratio(int Count, int Of, decimal? Percent)
{
    public static Ratio From(int count, int of) => new(count, of, of == 0 ? null : Math.Round(100m * count / of, 1));
}

public sealed record LeadTime(decimal? MedianDays, int Runs);

public sealed record QuotaActual(Category Category, int Base, int Winners);

/// <param name="ReferenceNo">The approved run the row is about; null when the space has none yet.</param>
public sealed record SpaceRow(
    Guid SpaceId, string Name, Guid? RoundId, DateOnly? StartsOn, DateOnly? EndsOn, string? ReferenceNo,
    int Locks, int Winners, int Confirmed, IReadOnlyList<QuotaActual> Quota);

public sealed record DashboardKpis(Ratio Occupancy, LeadTime LeadTime, Ratio NewVendors, Ratio FairRotation, IReadOnlyList<SpaceRow> Spaces);

/// <summary>
/// Executive dashboard (CLAUDE.md › API › KPI). For each space the round that counts is its latest round with an
/// approved allocation run — so a space whose new round is approved moves to it, and one with no run yet still
/// counts its locks with nobody confirmed.
/// <list type="bullet">
/// <item><b>Occupancy</b> = locks held by a winner who has confirmed ÷ all locks (all spaces).</item>
/// <item><b>LeadTime</b> = median days from the round's request close to the run's approval. A run approved before
/// requests closed (the demo does this) counts as 0 days.</item>
/// <item><b>NewVendors</b> = winners in the New group ÷ winners.</item>
/// <item><b>FairRotation</b> = winners who did not hold a lock at the same space in its previous round ÷ winners.
/// Where that previous round is not on record, the request's category stands in: anyone but Legacy (Legacy means held
/// a lock here last round).</item>
/// </list>
/// A winner is a request that is Won or Confirmed now, so forfeits and promotions show. Read-only, apart from settling
/// missed confirm deadlines first (ADR-0004).
/// </summary>
public sealed class GetDashboardHandler(IAppDbContext db, AssignmentExpiry expiry)
{
    public async Task<DashboardKpis> Handle(Guid? districtId, CancellationToken ct)
    {
        var spaces = await db.Spaces.AsNoTracking()
            .Where(s => s.IsActive && (districtId == null || s.DistrictId == districtId))
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, Locks = s.Locks.Count })
            .ToListAsync(ct);
        var spaceIds = spaces.Select(s => s.Id).ToList();

        var latest = (await (
                from run in db.AllocationRuns.AsNoTracking().Where(r => r.Status == AllocationRunStatus.Approved)
                join r in db.Rounds on run.RoundId equals r.Id
                where spaceIds.Contains(r.SpaceId)
                select new { Run = run, Round = r }).ToListAsync(ct))
            .GroupBy(x => x.Round.SpaceId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Round.StartsOn).First());
        var roundIds = latest.Values.Select(x => x.Round.Id).ToList();
        await expiry.ForfeitExpired(roundIds, ct);

        var winners = await db.StallRequests.AsNoTracking()
            .Where(q => roundIds.Contains(q.RoundId) && StallRequest.HeldLock.Contains(q.Status))
            .Select(q => new { q.RoundId, q.VendorId, q.Category })
            .ToListAsync(ct);
        var confirmed = await db.Assignments.AsNoTracking().Where(Assignment.Active)
            .Where(a => roundIds.Contains(a.RoundId) && a.ConfirmedAt != null)
            .GroupBy(a => a.RoundId).Select(g => new { RoundId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoundId, x => x.Count, ct);
        var heldBefore = await HeldLockInPreviousRounds(latest.Values.Select(x => (x.Round.SpaceId, x.Round.StartsOn)).ToList(), ct);

        var rows = spaces.Select(s =>
        {
            if (!latest.TryGetValue(s.Id, out var x))
                return new SpaceRow(s.Id, s.Name, null, null, null, null, s.Locks, 0, 0, []);
            var mine = winners.Where(w => w.RoundId == x.Round.Id).ToList();
            var quota = AllocationJson.Deserialize<AllocationResult>(x.Run.ResultJson).Quota
                .Select(q => new QuotaActual(q.Category, q.Base, mine.Count(w => w.Category == q.Category))).ToList();
            return new SpaceRow(s.Id, s.Name, x.Round.Id, x.Round.StartsOn, x.Round.EndsOn, x.Run.ReferenceNo,
                s.Locks, mine.Count, confirmed.GetValueOrDefault(x.Round.Id), quota);
        }).ToList();

        var spaceOfRound = latest.Values.ToDictionary(x => x.Round.Id, x => x.Round.SpaceId);
        var rotated = winners.Count(w => heldBefore.TryGetValue(spaceOfRound[w.RoundId], out var held)
            ? !held.Contains(w.VendorId)
            : w.Category != Category.Legacy);
        var leadDays = latest.Values
            .Select(x => Math.Max(0m, (decimal)(x.Run.DecidedAt!.Value - x.Round.ApplyClosesAt).TotalDays))
            .ToList();

        return new DashboardKpis(
            Ratio.From(rows.Sum(r => r.Confirmed), rows.Sum(r => r.Locks)),
            new LeadTime(Median(leadDays), leadDays.Count),
            Ratio.From(winners.Count(w => w.Category == Category.New), winners.Count),
            Ratio.From(rotated, winners.Count),
            rows);
    }

    /// <summary>Per space, who held a lock in the round before the counted one — only where that round is on record.</summary>
    private async Task<Dictionary<Guid, HashSet<Guid>>> HeldLockInPreviousRounds(
        List<(Guid SpaceId, DateOnly StartsOn)> counted, CancellationToken ct)
    {
        var result = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var (spaceId, startsOn) in counted)
        {
            // Only a decided round has winners to compare with; an earlier round never drawn falls back to Category.
            var previous = await db.Rounds.AsNoTracking()
                .Where(r => r.SpaceId == spaceId && r.EndsOn < startsOn && r.Status == RoundStatus.Approved)
                .OrderByDescending(r => r.EndsOn).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
            if (previous is null) continue;
            result[spaceId] = [.. await db.StallRequests.AsNoTracking()
                .Where(q => q.RoundId == previous && StallRequest.HeldLock.Contains(q.Status))
                .Select(q => q.VendorId).ToListAsync(ct)];
        }
        return result;
    }

    private static decimal? Median(List<decimal> values)
    {
        if (values.Count == 0) return null;
        var sorted = values.Order().ToList();
        var mid = sorted.Count / 2;
        var median = sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
        return Math.Round(median, 1);
    }
}
