using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Audit;
using SmartMarket.Domain.Reviews;
using SmartMarket.Domain.CheckIns;
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
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Use-case name for the save in progress; read by <see cref="Audit.AuditInterceptor"/>.</summary>
    internal string? PendingAuditAction { get; private set; }

    /// <summary>Explicit audit subject for the save in progress, if the command named one.</summary>
    internal AuditSubject? PendingAuditSubject { get; private set; }

    /// <summary>The save in progress is the system's own doing, not the current actor's.</summary>
    internal bool PendingAuditBySystem { get; private set; }

    /// <summary>Seed data is written without audit entries, as HasData seeding is (it is not anyone's command).</summary>
    internal bool SeedingWithoutAudit { get; private set; }

    internal async Task SaveSeedAsync(CancellationToken ct)
    {
        SeedingWithoutAudit = true;
        try
        {
            await SaveChangesAsync(ct);
        }
        finally
        {
            SeedingWithoutAudit = false;
        }
    }

    public Task CommitAsync(string action, CancellationToken ct) => Commit(action, null, false, ct);

    public Task CommitAsync(string action, AuditSubject subject, CancellationToken ct) => Commit(action, subject, false, ct);

    public Task CommitAsSystemAsync(string action, AuditSubject subject, CancellationToken ct) => Commit(action, subject, true, ct);

    public async Task<T> SerializedForVendorAsync<T>(Guid vendorId, Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await Database.BeginTransactionAsync(ct);
        // Row lock on the vendor until commit: a concurrent command for the same vendor blocks here, then reads committed
        // rows. NO KEY UPDATE leaves inserts that only reference the vendor (assignments, audit) unblocked.
        await Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Vendors\" WHERE \"Id\" = {vendorId} FOR NO KEY UPDATE", ct);
        var result = await work();
        await transaction.CommitAsync(ct);
        return result;
    }

    private async Task Commit(string action, AuditSubject? subject, bool bySystem, CancellationToken ct)
    {
        PendingAuditAction = action;
        PendingAuditSubject = subject;
        PendingAuditBySystem = bySystem;
        try
        {
            await SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            // Nothing was saved — the audit entry was part of the same SaveChanges, so it went too. Drop what is still
            // tracked so nothing in this request can save it by accident.
            ChangeTracker.Clear();
            throw new ConcurrentUpdateException(pg.ConstraintName, e);
        }
        finally
        {
            PendingAuditAction = null;
            PendingAuditSubject = null;
            PendingAuditBySystem = false;
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
