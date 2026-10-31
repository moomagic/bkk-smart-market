using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.CheckIns;

namespace SmartMarket.Infrastructure.Persistence.Configurations;

internal sealed class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> b)
    {
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.DeviceHash).HasMaxLength(64);
        b.HasOne<Assignment>().WithMany().HasForeignKey(x => x.AssignmentId);
        b.HasIndex(x => new { x.AssignmentId, x.Day }).IsUnique();
    }
}
