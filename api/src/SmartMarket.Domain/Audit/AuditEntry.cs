namespace SmartMarket.Domain.Audit;

/// <summary>
/// One row per command. <see cref="EntityType"/>/<see cref="EntityId"/> name the primary entity;
/// <see cref="Changes"/> holds the field-level diff of every entity the command touched
/// (personal data already masked).
/// </summary>
public sealed class AuditEntry
{
    public Guid Id { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string Actor { get; private set; }
    public string ActorRole { get; private set; }
    public string Action { get; private set; }
    public string EntityType { get; private set; }
    public string EntityId { get; private set; }
    public string Changes { get; private set; }

    public AuditEntry(
        Guid id, DateTimeOffset at, string actor, string actorRole, string action,
        string entityType, string entityId, string changes)
    {
        Id = id;
        At = at;
        Actor = actor;
        ActorRole = actorRole;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Changes = changes;
    }
}
