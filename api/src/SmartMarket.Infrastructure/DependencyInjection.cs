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
        services.AddOptions<AssistantOptions>()
            .Bind(config.GetSection(AssistantOptions.Section))
            .PostConfigure(o =>
            {
                // The environment variables apply only where config says nothing; an explicit Assistant:ApiKey = ""
                // switches the model off even on a machine that has ANTHROPIC_API_KEY set (tests rely on this).
                if (config[$"{AssistantOptions.Section}:ApiKey"] is null) o.ApiKey = config["ANTHROPIC_API_KEY"];
                if (config[$"{AssistantOptions.Section}:Model"] is null && config["ANTHROPIC_MODEL"] is { Length: > 0 } model)
                    o.Model = model;
            });
        services.AddHttpClient<ClaudeAssistantModel>();
        services.AddTransient<IAssistantModel>(sp => sp.GetRequiredService<ClaudeAssistantModel>());
        services.AddSingleton<IRulesLibrary, FileRulesLibrary>();
        services.AddOptions<RulesOptions>()
            .Bind(config.GetSection(RulesOptions.Section))
            .Validate(r => r.IsComplete(), $"appsettings section '{RulesOptions.Section}' is missing or incomplete")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DemoClock>();
        services.AddScoped<DemoReset>();
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
        await DatabaseSetup.ApplyAsync(scope.ServiceProvider.GetRequiredService<SmartMarketDbContext>(), ct);
    }
}
