using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Spaces;
using SmartMarket.Application.StallRequests;

namespace SmartMarket.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ListSpacesHandler>();
        services.AddScoped<SubmitStallRequestHandler>();
        services.AddScoped<ListMyStallRequestsHandler>();
        return services;
    }
}
