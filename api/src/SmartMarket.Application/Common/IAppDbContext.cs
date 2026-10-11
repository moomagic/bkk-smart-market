using Microsoft.EntityFrameworkCore;
using SmartMarket.Domain.Allocation;
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
    DbSet<AllocationRun> AllocationRuns { get; }
    DbSet<Assignment> Assignments { get; }
    DbSet<AuditEntry> AuditEntries { get; }

    /// <summary>
    /// Persists the command's changes and writes exactly one audit entry named <paramref name="action"/>
    /// (the use-case name, e.g. "StallRequest.Submit"). Call once per command.
    /// </summary>
    Task CommitAsync(string action, CancellationToken ct);

    /// <summary>
    /// As <see cref="CommitAsync(string, CancellationToken)"/>, but the audit entry is filed under
    /// <paramref name="subject"/> and is written even when nothing changed (e.g. a re-run eligibility check).
    /// </summary>
    Task CommitAsync(string action, AuditSubject subject, CancellationToken ct);

    /// <summary>
    /// As <see cref="CommitAsync(string, AuditSubject, CancellationToken)"/>, for what the system does on its own
    /// (e.g. forfeiting a missed deadline while someone reads): the audit entry names actor "system", not the reader.
    /// </summary>
    Task CommitAsSystemAsync(string action, AuditSubject subject, CancellationToken ct);
}
