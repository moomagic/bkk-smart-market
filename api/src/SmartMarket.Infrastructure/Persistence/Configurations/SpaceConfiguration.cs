using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartMarket.Domain.Districts;
using SmartMarket.Domain.Spaces;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Infrastructure.Persistence.Configurations;

internal sealed class SpaceConfiguration : IEntityTypeConfiguration<Space>
{
    public void Configure(EntityTypeBuilder<Space> b)
    {
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.OpenHours).HasMaxLength(100);
        b.Property(x => x.RulesRef).HasMaxLength(200);
        b.HasOne<District>().WithMany().HasForeignKey(x => x.DistrictId);
        b.HasMany(x => x.Locks).WithOne().HasForeignKey(x => x.SpaceId);
        b.Navigation(x => x.Locks).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasData(SeedData.Spaces);
    }
}

internal sealed class LockConfiguration : IEntityTypeConfiguration<Lock>
{
    public void Configure(EntityTypeBuilder<Lock> b)
    {
        b.Property(x => x.Code).HasMaxLength(10);
        b.HasIndex(x => new { x.SpaceId, x.Code }).IsUnique();
        b.HasData(SeedData.Locks);
    }
}
