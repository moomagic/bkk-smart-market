using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;
using SmartMarket.Domain.Vendors;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Infrastructure.Persistence.Configurations;

internal sealed class StallRequestConfiguration : IEntityTypeConfiguration<StallRequest>
{
    public void Configure(EntityTypeBuilder<StallRequest> b)
    {
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.IneligibleReason).HasMaxLength(500);
        b.HasOne<Round>().WithMany().HasForeignKey(x => x.RoundId);
        b.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId);
        b.HasIndex(x => new { x.RoundId, x.VendorId }).IsUnique();
        b.HasData(SeedData.StallRequests);
    }
}
