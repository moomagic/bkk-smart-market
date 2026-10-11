using SmartMarket.Api.Auth;
using SmartMarket.Application.Reviews;
using SmartMarket.Application.StallRequests;

namespace SmartMarket.Api.Endpoints;

public static class StallRequestsEndpoints
{
    public static IEndpointRouteBuilder MapStallRequests(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/stall-requests").RequireAuthorization(DemoPolicies.Vendor);

        group.MapPost("", async (SubmitStallRequest command, SubmitStallRequestHandler handler, CancellationToken ct) =>
            ResultHttp.Created(await handler.Handle(command, ct), r => $"/api/stall-requests/{r.Id}"));

        group.MapGet("/me", async (ListMyStallRequestsHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(ct)));

        group.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmStallRequestHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, ct)));

        group.MapPost("/{id:guid}/forfeit", async (Guid id, ForfeitStallRequestHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, ct)));

        group.MapPost("/{id:guid}/reviews", async (Guid id, FileReview command, FileReviewHandler handler, CancellationToken ct) =>
            ResultHttp.Created(await handler.Handle(id, command, ct), r => $"/api/reviews/{r.Id}"));

        return api;
    }
}
