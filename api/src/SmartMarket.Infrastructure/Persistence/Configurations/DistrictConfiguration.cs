using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartMarket.Domain.Districts;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Infrastructure.Persistence.Configurations;

internal sealed class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasData(SeedData.Districts);
    }
}
