using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Rounds;

namespace SmartMarket.Infrastructure.Persistence.Configurations;

internal sealed class AllocationRunConfiguration : IEntityTypeConfiguration<AllocationRun>
{
    public void Configure(EntityTypeBuilder<AllocationRun> b)
    {
        b.Property(x => x.ReferenceNo).HasMaxLength(30);
        b.Property(x => x.InputHash).HasMaxLength(64);
        b.Property(x => x.RulesSnapshot).HasColumnType("jsonb");
        b.Property(x => x.ResultJson).HasColumnType("jsonb");
        b.Property(x => x.RanBy).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RejectReason).HasMaxLength(500);
        b.Property(x => x.DecidedBy).HasMaxLength(100);
        b.HasOne<Round>().WithMany().HasForeignKey(x => x.RoundId);
        b.HasIndex(x => x.ReferenceNo).IsUnique();
        // At most one draft per round, even when an officer double-clicks.
        b.HasIndex(x => x.RoundId).IsUnique().HasFilter("\"Status\" = 'Draft'");
    }
}
