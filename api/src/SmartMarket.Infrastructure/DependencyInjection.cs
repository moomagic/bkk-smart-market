using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Assistant;
using SmartMarket.Application.Audit;
using SmartMarket.Application.Common;
using SmartMarket.Infrastructure.Allocation;
using SmartMarket.Infrastructure.Assistant;
using SmartMarket.Infrastructure.Audit;
using SmartMarket.Infrastructure.Persistence;
using SmartMarket.Infrastructure.Seed;
using SmartMarket.Infrastructure.Time;

namespace SmartMarket.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DemoOptions>().Bind(config.GetSection(DemoOptions.Section));
        services.AddOptions<AssistantOptions>().Bind(config.GetSection(AssistantOptions.Section));
        services.AddSingleton<IRulesLibrary, FileRulesLibrary>();
        services.AddOptions<RulesOptions>()
            .Bind(config.GetSection(RulesOptions.Section))
            .Validate(r => r.IsComplete(), $"appsettings section '{RulesOptions.Section}' is missing or incomplete")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DemoClock>();
        services.AddSingleton<IDemoClock>(sp => sp.GetRequiredService<DemoClock>());
        services.AddSingleton<IClock>(sp => sp.GetRequiredService<IDemoClock>());
        services.AddSingleton<ISeedSource>(_ => new DemoSeedSource(new CryptoSeedSource()));

        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<SmartMarketDbContext>((sp, options) => options
            // Never EnableSensitiveDataLogging: parameter values may carry personal data (PDPA).
            .UseNpgsql(config.GetConnectionString("SmartMarket"))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<SmartMarketDbContext>());
        services.AddScoped<IAuditTrail, AuditTrail>();
        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartMarketDbContext>();
        await db.Database.MigrateAsync(ct);
        await HistorySeed.ApplyAsync(db, ct);
    }
}
