using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;

namespace SmartMarket.Application.Reviews;

/// <summary>Every review of a round, open ones first, with the date the district should answer by.</summary>
public sealed class ListRoundReviewsHandler(IAppDbContext db, IOptions<RulesOptions> rules)
{
    public async Task<Result<IReadOnlyList<RoundReviewRow>>> Handle(Guid roundId, CancellationToken ct)
    {
        if (!await db.Rounds.AnyAsync(r => r.Id == roundId, ct))
            return Error.NotFound("round.not_found", "ไม่พบรอบนี้");
        var days = rules.Value.Review.RespondWithinDays;
        var rows = await (
            from r in db.Reviews.AsNoTracking()
            join q in db.StallRequests on r.StallRequestId equals q.Id
            join v in db.Vendors on q.VendorId equals v.Id
            where q.RoundId == roundId
            orderby r.Outcome != null, r.SubmittedAt
            select new { Review = r, q.Category, q.Status, q.IneligibleReason, v.MaskedName }).ToListAsync(ct);
        return rows.Select(x => new RoundReviewRow(
            x.Review.Id, x.Review.StallRequestId, x.MaskedName, x.Category, x.Status, x.IneligibleReason, x.Review.Reason,
            x.Review.SubmittedAt, x.Review.SubmittedAt.AddDays(days), x.Review.Outcome, x.Review.Response, x.Review.RespondedAt)).ToList();
    }
}
