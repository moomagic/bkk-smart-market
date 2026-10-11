using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;

namespace SmartMarket.Application.Audit;

/// <param name="ActorName">For a vendor, the masked name (Guardrail 5); otherwise the actor id itself.</param>
/// <param name="Changes">The stored change list, personal data already masked by the audit interceptor.</param>
public sealed record AuditEntryDto(
    Guid Id, DateTimeOffset At, string Actor, string ActorName, string ActorRole, string Action,
    string EntityType, string EntityId, JsonElement Changes);

/// <summary>Officer's audit timeline per round or per entity (CLAUDE.md › Guardrail 3).</summary>
public sealed class GetAuditTrailHandler(IAppDbContext db, IAuditTrail trail)
{
    public const string VendorRole = "vendor";

    public async Task<Result<IReadOnlyList<AuditEntryDto>>> Handle(string? entityType, string? entityId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            return Error.Invalid("audit.entity_type_required", "กรุณาระบุประเภทข้อมูล (entityType)");
        // Ids are stored as Guid.ToString(); accept any casing or format the caller sends.
        var id = string.IsNullOrWhiteSpace(entityId) ? null
            : Guid.TryParse(entityId, out var g) ? g.ToString() : entityId.Trim();
        var entries = await trail.About(entityType.Trim(), id, ct);

        // A vendor acts under their VendorId; show the masked name instead (Guardrail 5).
        var vendorIds = entries.Where(e => e.ActorRole == VendorRole)
            .Select(e => Guid.TryParse(e.Actor, out var v) ? v : (Guid?)null).OfType<Guid>().Distinct().ToList();
        var names = await db.Vendors.AsNoTracking().Where(v => vendorIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id.ToString(), v => v.MaskedName, ct);

        return entries.Select(e => new AuditEntryDto(
            e.Id, e.At, e.Actor, names.GetValueOrDefault(e.Actor, e.Actor), e.ActorRole, e.Action, e.EntityType, e.EntityId,
            JsonDocument.Parse(e.Changes).RootElement.Clone())).ToList();
    }
}
