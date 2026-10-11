using Microsoft.EntityFrameworkCore;
using SmartMarket.Infrastructure.Persistence;

namespace SmartMarket.Infrastructure.Seed;

/// <summary>Brings a database to the current schema and seed state — on every start, and after a demo reset.</summary>
internal static class DatabaseSetup
{
    public static async Task ApplyAsync(SmartMarketDbContext db, CancellationToken ct)
    {
        await db.Database.MigrateAsync(ct); // schema + HasData seed
        await HistorySeed.ApplyAsync(db, ct); // engine-drawn previous rounds, once
    }
}
