using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Audit;

namespace SmartMarket.Api.Tests;

/// <summary>
/// "Which audit entries did this action write?" Compared by id, not timestamp: tests that move the clock forward
/// leave entries dated in the future, so "after the latest At" would miss everything written at the demo time.
/// </summary>
internal static class AuditProbe
{
    public static async Task<HashSet<Guid>> AuditMark(this ApiFactory api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return [.. await db.AuditEntries.Select(a => a.Id).ToListAsync()];
    }

    public static async Task<List<AuditEntry>> AuditSince(this ApiFactory api, HashSet<Guid> mark)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return [.. (await db.AuditEntries.ToListAsync()).Where(a => !mark.Contains(a.Id)).OrderBy(a => a.At)];
    }
}
