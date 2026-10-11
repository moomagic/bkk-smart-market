using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;
using SmartMarket.Infrastructure.Persistence;
using SmartMarket.Infrastructure.Time;

namespace SmartMarket.Infrastructure.Seed;

/// <summary>
/// Puts the demo back where it starts (Development only — POST /api/demo/reset). Every table is dropped and the
/// database is built again exactly as on a first start — migrations (which carry the HasData seed), then the
/// engine-drawn history — so the reset needs no knowledge of what the demo added. (The #9 review wanted a path that
/// avoids re-running migrations because HasData lives in them; rebuilding the schema re-runs them from empty instead,
/// which is the same thing a fresh install does.) The clock goes back to Demo:Now, and the reset itself is the first
/// entry of the new audit trail.
/// </summary>
public sealed class DemoReset(SmartMarketDbContext db, IDemoClock clock, IOptions<DemoOptions> options)
{
    public const string Action = "Demo.Reset";

    public async Task ResetAsync(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;", ct);
        db.ChangeTracker.Clear();
        await DatabaseSetup.ApplyAsync(db, ct);
        if (options.Value.Now is { } start) clock.Set(start);
        await db.CommitAsync(Action, new AuditSubject("Demo", "reset"), ct);
    }
}
