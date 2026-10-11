using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Audit;
using SmartMarket.Domain.Districts;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.Spaces;
using SmartMarket.Domain.StallRequests;
using SmartMarket.Domain.Vendors;

namespace SmartMarket.Infrastructure.Persistence;

public sealed class SmartMarketDbContext(DbContextOptions<SmartMarketDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<District> Districts => Set<District>();
    public DbSet<Space> Spaces => Set<Space>();
    public DbSet<Lock> Locks => Set<Lock>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<StallRequest> StallRequests => Set<StallRequest>();
    public DbSet<AllocationRun> AllocationRuns => Set<AllocationRun>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Use-case name for the save in progress; read by <see cref="Audit.AuditInterceptor"/>.</summary>
    internal string? PendingAuditAction { get; private set; }

    /// <summary>Explicit audit subject for the save in progress, if the command named one.</summary>
    internal AuditSubject? PendingAuditSubject { get; private set; }

    public Task CommitAsync(string action, CancellationToken ct) => Commit(action, null, ct);

    public Task CommitAsync(string action, AuditSubject subject, CancellationToken ct) => Commit(action, subject, ct);

    private async Task Commit(string action, AuditSubject? subject, CancellationToken ct)
    {
        PendingAuditAction = action;
        PendingAuditSubject = subject;
        try
        {
            await SaveChangesAsync(ct);
        }
        finally
        {
            PendingAuditAction = null;
            PendingAuditSubject = null;
        }
    }

    /// <summary>timestamptz only accepts UTC offsets; normalise here so callers may pass Bangkok time.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();

    private sealed class UtcDateTimeOffsetConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, DateTimeOffset>(
        v => v.ToUniversalTime(), v => v);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartMarketDbContext).Assembly);
}
