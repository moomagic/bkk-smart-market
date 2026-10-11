using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Audit;
using SmartMarket.Domain.Audit;
using SmartMarket.Infrastructure.Persistence;

namespace SmartMarket.Infrastructure.Audit;

/// <summary>Matches entities inside the Changes jsonb with PostgreSQL containment (<c>@&gt;</c>).</summary>
public sealed class AuditTrail(SmartMarketDbContext db) : IAuditTrail
{
    public async Task<IReadOnlyList<AuditEntry>> About(string entityType, string? entityId, CancellationToken ct)
    {
        var anyOfType = Contains(new { Entity = entityType });
        IQueryable<AuditEntry> query;
        if (entityId is null)
        {
            query = db.AuditEntries.Where(a => a.EntityType == entityType || EF.Functions.JsonContains(a.Changes, anyOfType));
        }
        else
        {
            var itself = Contains(new { Entity = entityType, Id = entityId });
            var foreignKey = $"{entityType}Id";
            var childAfter = Contains(new { After = new Dictionary<string, string> { [foreignKey] = entityId } });
            var childBefore = Contains(new { Before = new Dictionary<string, string> { [foreignKey] = entityId } });
            query = db.AuditEntries.Where(a =>
                (a.EntityType == entityType && a.EntityId == entityId)
                || EF.Functions.JsonContains(a.Changes, itself)
                || EF.Functions.JsonContains(a.Changes, childAfter)
                || EF.Functions.JsonContains(a.Changes, childBefore));
        }
        return await query.AsNoTracking().OrderBy(a => a.At).ThenBy(a => a.Id).ToListAsync(ct);
    }

    /// <summary>Changes is a JSON array of change objects; containment needs the probe wrapped the same way.</summary>
    private static string Contains(object change) => JsonSerializer.Serialize(new[] { change });
}
