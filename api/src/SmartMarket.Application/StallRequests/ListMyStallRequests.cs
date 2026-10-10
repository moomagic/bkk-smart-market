using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;

namespace SmartMarket.Application.StallRequests;

public sealed class ListMyStallRequestsHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<Result<IReadOnlyList<StallRequestDto>>> Handle(CancellationToken ct)
    {
        if (await VendorLookup.Current(db, actor, ct) is not { } vendor)
            return VendorLookup.Unknown;
        var vendorId = vendor.Id;
        return await (
            from req in db.StallRequests.AsNoTracking()
            join r in db.Rounds on req.RoundId equals r.Id
            join s in db.Spaces on r.SpaceId equals s.Id
            where req.VendorId == vendorId
            orderby r.StartsOn descending
            select new StallRequestDto(
                req.Id, r.Id, s.Id, s.Name, r.StartsOn, r.EndsOn, req.SubmittedAt, req.Category, req.Status,
                req.IneligibleReason)).ToListAsync(ct);
    }
}
