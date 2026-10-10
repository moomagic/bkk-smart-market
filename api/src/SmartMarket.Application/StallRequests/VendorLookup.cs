using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Vendors;

namespace SmartMarket.Application.StallRequests;

/// <summary>The vendor behind the current demo user (X-Demo-User holds the VendorId).</summary>
internal static class VendorLookup
{
    public static readonly Error Unknown = Error.Forbidden("vendor.unknown", "ไม่พบข้อมูลผู้ค้า");

    public static async Task<Vendor?> Current(IAppDbContext db, ICurrentActor actor, CancellationToken ct) =>
        Guid.TryParse(actor.Actor, out var id)
            ? await db.Vendors.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id, ct)
            : null;
}
