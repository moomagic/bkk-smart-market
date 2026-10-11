using SmartMarket.Api.Auth;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Reviews;
using SmartMarket.Application.Rounds;

namespace SmartMarket.Api.Endpoints;

public static class RoundsEndpoints
{
    public static IEndpointRouteBuilder MapRounds(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/rounds").RequireAuthorization(DemoPolicies.Officer);

        group.MapGet("", (ListRoundsHandler handler, CancellationToken ct) => handler.Handle(ct));

        group.MapGet("/{id:guid}/stall-requests", async (Guid id, ListRoundRequestsHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, ct)));

        group.MapPost("/{id:guid}/eligibility", async (Guid id, CheckRoundEligibilityHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, ct)));

        group.MapGet("/{id:guid}/allocation-runs", async (Guid id, ListRoundAllocationRunsHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, ct)));

        // No seed parameter by design: the system picks it (ADR-0002).
        group.MapPost("/{id:guid}/allocation-runs", async (Guid id, RunAllocationHandler handler, CancellationToken ct) =>
            ResultHttp.Created(await handler.Handle(id, ct), run => $"/api/allocation-runs/{run.Id}"));

        group.MapGet("/{id:guid}/reviews", async (Guid id, ListRoundReviewsHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, ct)));

        return api;
    }
}
