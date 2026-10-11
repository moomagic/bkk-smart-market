using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.Spaces;
using SmartMarket.Domain.Vendors;

namespace SmartMarket.Infrastructure.Persistence.Configurations;

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> b)
    {
        b.HasOne<Round>().WithMany().HasForeignKey(x => x.RoundId);
        b.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId);
        b.HasOne<Lock>().WithMany().HasForeignKey(x => x.LockId);
        // One lock per vendor per round, one holder per lock per round.
        b.HasIndex(x => new { x.RoundId, x.VendorId }).IsUnique();
        b.HasIndex(x => new { x.RoundId, x.LockId }).IsUnique();
    }
}
