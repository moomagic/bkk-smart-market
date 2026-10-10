using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Audit;
using SmartMarket.Domain.Common;
using SmartMarket.Infrastructure.Persistence;

namespace SmartMarket.Infrastructure.Audit;

/// <summary>
/// Writes exactly one <see cref="AuditEntry"/> per SaveChanges that changes anything.
/// Commands go through <see cref="IAppDbContext.CommitAsync"/>, which names the action;
/// a bare SaveChanges is still audited as "Unscoped" so nothing slips through.
/// Personal data is masked before it reaches the audit row.
/// </summary>
public sealed class AuditInterceptor(IClock clock, ICurrentActor actor) : SaveChangesInterceptor
{
    public const string UnscopedAction = "Unscoped";
    internal const string Masked = "***";

    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditEntry(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddAuditEntry(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void AddAuditEntry(DbContext? context)
    {
        if (context is null) return;

        var changes = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditEntry
                        && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(Describe)
            .Where(c => c is not null)
            .Cast<EntityChange>()
            .ToList();
        var smartMarket = context as SmartMarketDbContext;
        var subject = smartMarket?.PendingAuditSubject;
        if (changes.Count == 0 && subject is null) return;

        var action = smartMarket?.PendingAuditAction ?? UnscopedAction;
        var (entityType, entityId) = subject is not null
            ? (subject.EntityType, subject.EntityId)
            : (changes[0].Entity, changes[0].Id);
        context.Add(new AuditEntry(
            Guid.CreateVersion7(clock.Now), clock.Now, actor.Actor, actor.Role, action,
            entityType, entityId, JsonSerializer.Serialize(changes, Json)));
    }

    private static EntityChange? Describe(EntityEntry entry)
    {
        var props = entry.Properties
            .Where(p => !p.Metadata.IsPrimaryKey())
            .Where(p => entry.State != EntityState.Modified || p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
            .ToList();
        if (entry.State == EntityState.Modified && props.Count == 0) return null;

        Dictionary<string, object?>? before = entry.State is EntityState.Added ? null : [];
        Dictionary<string, object?>? after = entry.State is EntityState.Deleted ? null : [];
        foreach (var p in props)
        {
            var personal = p.Metadata.PropertyInfo?.GetCustomAttribute<PersonalDataAttribute>() is not null;
            before?.Add(p.Metadata.Name, personal ? MaskValue(p.OriginalValue) : p.OriginalValue);
            after?.Add(p.Metadata.Name, personal ? MaskValue(p.CurrentValue) : p.CurrentValue);
        }

        var id = string.Join(",", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue));
        return new EntityChange(entry.Metadata.ClrType.Name, id, entry.State.ToString(), before, after);
    }

    private static string? MaskValue(object? value) => value is null ? null : Masked;

    private sealed record EntityChange(
        string Entity, string Id, string State,
        Dictionary<string, object?>? Before, Dictionary<string, object?>? After);
}
