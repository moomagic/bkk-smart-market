using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Audit;
using SmartMarket.Application.CheckIns;
using SmartMarket.Application.Rounds;
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
        services.AddScoped<ListRoundsHandler>();
        services.AddScoped<ListRoundRequestsHandler>();
        services.AddScoped<CheckRoundEligibilityHandler>();
        services.AddScoped<RunAllocationHandler>();
        services.AddScoped<GetAllocationRunHandler>();
        services.AddScoped<ApproveAllocationRunHandler>();
        services.AddScoped<AssignmentExpiry>();
        services.AddScoped<ConfirmStallRequestHandler>();
        services.AddScoped<ForfeitStallRequestHandler>();
        services.AddScoped<CheckInAtLockHandler>();
        services.AddScoped<GetAuditTrailHandler>();
        services.AddScoped<ListRoundAllocationRunsHandler>();
        return services;
    }
}
