namespace SmartMarket.Application.Common;

/// <summary>The entity an audit entry is filed under when a command acts on a whole aggregate (e.g. a Round).</summary>
public sealed record AuditSubject(string EntityType, string EntityId)
{
    public static AuditSubject For<T>(Guid id) => new(typeof(T).Name, id.ToString());
}
