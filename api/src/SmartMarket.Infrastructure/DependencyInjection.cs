using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Common;
using SmartMarket.Infrastructure.Allocation;
using SmartMarket.Infrastructure.Audit;
using SmartMarket.Infrastructure.Persistence;
using SmartMarket.Infrastructure.Time;

namespace SmartMarket.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DemoOptions>().Bind(config.GetSection(DemoOptions.Section));
        services.AddOptions<RulesOptions>()
            .Bind(config.GetSection(RulesOptions.Section))
            .Validate(r => r.IsComplete(), $"appsettings section '{RulesOptions.Section}' is missing or incomplete")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, DemoClock>();
        services.AddSingleton<ISeedSource>(_ => new DemoSeedSource(new CryptoSeedSource()));

        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<SmartMarketDbContext>((sp, options) => options
            // Never EnableSensitiveDataLogging: parameter values may carry personal data (PDPA).
            .UseNpgsql(config.GetConnectionString("SmartMarket"))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<SmartMarketDbContext>());
        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SmartMarketDbContext>().Database.MigrateAsync(ct);
    }
}
