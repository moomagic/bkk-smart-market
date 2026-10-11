using SmartMarket.Api.Auth;
using SmartMarket.Application.Allocation;

namespace SmartMarket.Api.Endpoints;

public static class AllocationRunsEndpoints
{
    public static IEndpointRouteBuilder MapAllocationRuns(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/allocation-runs").RequireAuthorization(DemoPolicies.Officer);

        group.MapGet("/{id:guid}", async (Guid id, GetAllocationRunHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, ct)));

        group.MapPost("/{id:guid}/approve", async (Guid id, ApproveAllocationRunHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, ct)));

        return api;
    }
}
