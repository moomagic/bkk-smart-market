using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Spaces;

namespace SmartMarket.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ListSpacesHandler>();
        return services;
    }
}
