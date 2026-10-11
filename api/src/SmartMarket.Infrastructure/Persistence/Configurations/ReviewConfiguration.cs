using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartMarket.Domain.Reviews;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Infrastructure.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.Response).HasMaxLength(1000);
        b.Property(x => x.RespondedBy).HasMaxLength(100);
        b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20);
        b.HasOne<StallRequest>().WithMany().HasForeignKey(x => x.StallRequestId);
        // One review per request (ADR-0005).
        b.HasIndex(x => x.StallRequestId).IsUnique();
    }
}
