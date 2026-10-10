using SmartMarket.Api.Auth;
using SmartMarket.Application.Common;
using SmartMarket.Application.StallRequests;

namespace SmartMarket.Api.Endpoints;

public static class StallRequestsEndpoints
{
    public static IEndpointRouteBuilder MapStallRequests(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/stall-requests").RequireAuthorization(DemoPolicies.Vendor);

        group.MapPost("", async (SubmitStallRequest command, ICurrentActor actor, SubmitStallRequestHandler handler, CancellationToken ct) =>
        {
            if (!Guid.TryParse(actor.Actor, out var vendorId))
                return ResultHttp.Problem(Error.Forbidden("vendor.unknown", "ไม่พบข้อมูลผู้ค้า"));
            var result = await handler.Handle(vendorId, command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/stall-requests/{result.Value.Id}", result.Value)
                : ResultHttp.Problem(result.Error!);
        });

        group.MapGet("/me", async (ICurrentActor actor, ListMyStallRequestsHandler handler, CancellationToken ct) =>
            Guid.TryParse(actor.Actor, out var vendorId)
                ? Results.Ok(await handler.Handle(vendorId, ct))
                : ResultHttp.Problem(Error.Forbidden("vendor.unknown", "ไม่พบข้อมูลผู้ค้า")));

        return api;
    }
}
