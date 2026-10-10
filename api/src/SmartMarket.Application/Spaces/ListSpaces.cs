using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Spaces;

namespace SmartMarket.Application.Spaces;

public sealed record SpaceSummary(
    Guid Id, string Name, SpaceType Type, int LockCount, IReadOnlyList<string> ProductTypes, string OpenHours);

public sealed class ListSpacesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<SpaceSummary>> Handle(Guid? districtId, SpaceType? type, CancellationToken ct) =>
        await db.Spaces
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Where(s => districtId == null || s.DistrictId == districtId)
            .Where(s => type == null || s.Type == type)
            .OrderBy(s => s.Name)
            .Select(s => new SpaceSummary(s.Id, s.Name, s.Type, s.Locks.Count, s.ProductTypes, s.OpenHours))
            .ToListAsync(ct);
}
