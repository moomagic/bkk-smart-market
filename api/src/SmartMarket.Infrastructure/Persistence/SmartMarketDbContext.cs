using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Audit;
using SmartMarket.Domain.Districts;
using SmartMarket.Domain.Spaces;
using SmartMarket.Domain.Vendors;

namespace SmartMarket.Infrastructure.Persistence;

public sealed class SmartMarketDbContext(DbContextOptions<SmartMarketDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<District> Districts => Set<District>();
    public DbSet<Space> Spaces => Set<Space>();
    public DbSet<Lock> Locks => Set<Lock>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Use-case name for the save in progress; read by <see cref="Audit.AuditInterceptor"/>.</summary>
    internal string? PendingAuditAction { get; private set; }

    public async Task CommitAsync(string action, CancellationToken ct)
    {
        PendingAuditAction = action;
        try
        {
            await SaveChangesAsync(ct);
        }
        finally
        {
            PendingAuditAction = null;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartMarketDbContext).Assembly);
}
