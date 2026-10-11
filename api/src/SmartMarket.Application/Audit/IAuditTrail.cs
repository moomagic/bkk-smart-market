using SmartMarket.Domain.Audit;

namespace SmartMarket.Application.Audit;

/// <summary>Reads audit entries about an entity (the query needs database-specific JSON operators, so it lives in Infrastructure).</summary>
public interface IAuditTrail
{
    /// <summary>
    /// Entries, oldest first, that touched <paramref name="entityType"/> (and <paramref name="entityId"/>, if given):
    /// as the command's primary entity, as any entity in its Changes, or — with an id — as the parent of a changed
    /// entity whose <c>{entityType}Id</c> is that id (so a Round's trail includes requests submitted to it).
    /// The parent match sees a foreign key only where the change lists it — always for a creation, but for an edit only
    /// when that key itself changed — so commands on a child should name the parent as their subject (round commands do).
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> About(string entityType, string? entityId, CancellationToken ct);
}
