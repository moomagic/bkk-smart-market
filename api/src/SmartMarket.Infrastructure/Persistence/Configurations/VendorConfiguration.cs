using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartMarket.Domain.Vendors;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Infrastructure.Persistence.Configurations;

internal sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> b)
    {
        b.Property(x => x.DisplayName).HasMaxLength(200);
        b.Property(x => x.MaskedName).HasMaxLength(100);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.VulnerableReason).HasMaxLength(100);
        b.Property(x => x.ProductType).HasMaxLength(100);
        b.HasData(SeedData.Vendors);
    }
}
