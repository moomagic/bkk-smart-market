using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;

namespace SmartMarket.Application.Rounds;

internal static class RoundQueries
{
    /// <summary>The round at the same space that ended last before <paramref name="startsOn"/> — "the previous round".</summary>
    public static Task<Guid?> PreviousRoundId(IAppDbContext db, Guid spaceId, DateOnly startsOn, CancellationToken ct) =>
        db.Rounds
            .Where(r => r.SpaceId == spaceId && r.EndsOn < startsOn)
            .OrderByDescending(r => r.EndsOn)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
}
