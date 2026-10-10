using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.Spaces;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Infrastructure.Persistence.Configurations;

internal sealed class RoundConfiguration : IEntityTypeConfiguration<Round>
{
    public void Configure(EntityTypeBuilder<Round> b)
    {
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne<Space>().WithMany().HasForeignKey(x => x.SpaceId);
        b.HasIndex(x => new { x.SpaceId, x.StartsOn }).IsUnique();
        b.HasData(SeedData.Rounds);
    }
}
