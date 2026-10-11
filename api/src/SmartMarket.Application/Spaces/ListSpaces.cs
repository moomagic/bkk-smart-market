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
    public Task<List<SpaceSummary>> Handle(Guid? districtId, SpaceType? type, CancellationToken ct) =>
        Query(s => (districtId == null || s.DistrictId == districtId) && (type == null || s.Type == type), ct);

    public async Task<Result<SpaceSummary>> Handle(Guid spaceId, CancellationToken ct) =>
        (await Query(s => s.Id == spaceId, ct)).SingleOrDefault() is { } space
            ? space
            : Error.NotFound("space.not_found", "ไม่พบพื้นที่นี้");

    private async Task<List<SpaceSummary>> Query(System.Linq.Expressions.Expression<Func<Space, bool>> filter, CancellationToken ct)
    {
        var now = clock.Now;
        return await db.Spaces
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Where(filter)
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
