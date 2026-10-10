using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartMarket.Domain.Audit;

namespace SmartMarket.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.Actor).HasMaxLength(100);
        b.Property(x => x.ActorRole).HasMaxLength(30);
        b.Property(x => x.Action).HasMaxLength(100);
        b.Property(x => x.EntityType).HasMaxLength(100);
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.Changes).HasColumnType("jsonb");
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.At);
    }
}
