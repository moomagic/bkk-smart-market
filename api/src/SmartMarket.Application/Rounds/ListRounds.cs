using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Rounds;

public sealed class ListRoundsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<RoundSummary>> Handle(CancellationToken ct) =>
        await (
            from r in db.Rounds.AsNoTracking()
            join s in db.Spaces on r.SpaceId equals s.Id
            orderby r.StartsOn descending, s.Name
            select new RoundSummary(
                r.Id, s.Id, s.Name, r.StartsOn, r.EndsOn, r.ApplyClosesAt, r.Status,
                db.StallRequests.Count(q => q.RoundId == r.Id),
                db.StallRequests.Count(q => q.RoundId == r.Id && q.Status == StallRequestStatus.Eligible),
                db.StallRequests.Count(q => q.RoundId == r.Id && q.Status == StallRequestStatus.Ineligible)))
            .ToListAsync(ct);
}

public sealed class ListRoundRequestsHandler(IAppDbContext db)
{
    public async Task<Result<IReadOnlyList<RoundRequestRow>>> Handle(Guid roundId, CancellationToken ct)
    {
        if (!await db.Rounds.AnyAsync(r => r.Id == roundId, ct))
            return Error.NotFound("round.not_found", "ไม่พบรอบนี้");
        return await Rows(db, roundId, ct);
    }

    internal static async Task<List<RoundRequestRow>> Rows(IAppDbContext db, Guid roundId, CancellationToken ct) =>
        await (
            from q in db.StallRequests.AsNoTracking()
            join v in db.Vendors on q.VendorId equals v.Id
            where q.RoundId == roundId
            // quota order (Legacy → Vulnerable → New), not the alphabetical order of the stored strings
            orderby q.Category == Category.Legacy ? 0 : q.Category == Category.Vulnerable ? 1 : 2, q.SubmittedAt
            select new RoundRequestRow(q.Id, v.Id, v.MaskedName, q.Category, q.Status, q.IneligibleReason, q.CarryOverWeight))
            .ToListAsync(ct);
}
