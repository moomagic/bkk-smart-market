using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.Spaces;

namespace SmartMarket.Application.Spaces;

public sealed record OpenRoundSummary(Guid Id, DateOnly StartsOn, DateOnly EndsOn, DateTimeOffset ApplyClosesAt);

public sealed record SpaceSummary(
    Guid Id, string Name, SpaceType Type, int LockCount, IReadOnlyList<string> ProductTypes, string OpenHours,
    OpenRoundSummary? OpenRound);

public sealed class ListSpacesHandler(IAppDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<SpaceSummary>> Handle(Guid? districtId, SpaceType? type, CancellationToken ct)
    {
        var now = clock.Now;
        return await db.Spaces
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Where(s => districtId == null || s.DistrictId == districtId)
            .Where(s => type == null || s.Type == type)
            .OrderBy(s => s.Name)
            .Select(s => new SpaceSummary(
                s.Id, s.Name, s.Type, s.Locks.Count, s.ProductTypes, s.OpenHours,
                db.Rounds
                    .Where(Round.AcceptingRequestsAt(now))
                    .Where(r => r.SpaceId == s.Id)
                    .OrderBy(r => r.StartsOn)
                    .Select(r => new OpenRoundSummary(r.Id, r.StartsOn, r.EndsOn, r.ApplyClosesAt))
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }
}
