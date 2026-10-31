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

        group.MapPost("/{id:guid}/reject", async (Guid id, RejectAllocationRun command, RejectAllocationRunHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, command, ct)));

        // Outside the officer group: vendors verify the runs of their own rounds too (the handler checks which)
        api.MapGet("/allocation-runs/{id:guid}/verify", async (Guid id, VerifyAllocationRunHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, ct))).RequireAuthorization();

        return api;
    }
}
