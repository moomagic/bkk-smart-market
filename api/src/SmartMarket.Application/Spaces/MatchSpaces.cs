using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Rounds;

namespace SmartMarket.Application.Spaces;

/// <summary>Top 3 spaces for a vendor's goods and preferences, with reasons (see <see cref="SpaceMatcher"/>). Read-only.</summary>
public sealed class MatchSpacesHandler(IAppDbContext db, IClock clock)
{
    public async Task<Result<IReadOnlyList<SpaceMatch>>> Handle(MatchQuery query, Guid? districtId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.ProductType))
            return Error.Invalid("match.product_required", "กรุณาระบุประเภทสินค้าที่ขาย");

        var now = clock.Now;
        var candidates = await db.Spaces
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Where(s => districtId == null || s.DistrictId == districtId)
            .Select(s => new
            {
                s.Id, s.Name, s.ProductTypes, s.OpenHours, LockCount = s.Locks.Count,
                OpenRound = db.Rounds
                    .Where(Round.AcceptingRequestsAt(now))
                    .Where(r => r.SpaceId == s.Id)
                    .OrderBy(r => r.StartsOn)
                    .Select(r => new
                    {
                        Summary = new OpenRoundSummary(r.Id, r.StartsOn, r.EndsOn, r.ApplyClosesAt),
                        Requests = db.StallRequests.Count(q => q.RoundId == r.Id),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return SpaceMatcher.Rank(query, candidates.Select(c => new MatchCandidate(
            c.Id, c.Name, c.ProductTypes, c.OpenHours, c.OpenRound?.Summary, c.LockCount, c.OpenRound?.Requests ?? 0))).ToList();
    }
}
