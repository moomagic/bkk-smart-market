using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartMarket.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef` only.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmartMarketDbContext>
{
    public SmartMarketDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SmartMarketDbContext>()
            .UseNpgsql("Host=localhost;Database=smartmarket;Username=postgres;Password=postgres")
            .Options);
}
