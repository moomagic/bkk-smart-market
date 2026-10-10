using Microsoft.EntityFrameworkCore;
using SmartMarket.Domain.Audit;
using SmartMarket.Domain.Districts;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.Spaces;
using SmartMarket.Domain.StallRequests;
using SmartMarket.Domain.Vendors;

namespace SmartMarket.Application.Common;

public interface IAppDbContext
{
    DbSet<District> Districts { get; }
    DbSet<Space> Spaces { get; }
    DbSet<Lock> Locks { get; }
    DbSet<Vendor> Vendors { get; }
    DbSet<Round> Rounds { get; }
    DbSet<StallRequest> StallRequests { get; }
    DbSet<AuditEntry> AuditEntries { get; }

    /// <summary>
    /// Persists the command's changes and writes exactly one audit entry named <paramref name="action"/>
    /// (the use-case name, e.g. "StallRequest.Submit"). Call once per command.
    /// </summary>
    Task CommitAsync(string action, CancellationToken ct);
}
