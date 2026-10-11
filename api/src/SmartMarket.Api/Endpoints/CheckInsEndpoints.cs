using SmartMarket.Api.Auth;
using SmartMarket.Application.CheckIns;

namespace SmartMarket.Api.Endpoints;

public static class CheckInsEndpoints
{
    public static IEndpointRouteBuilder MapCheckIns(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/check-ins").RequireAuthorization(DemoPolicies.Vendor);

        group.MapPost("", async (CheckInAtLock command, CheckInAtLockHandler handler, CancellationToken ct) =>
            ResultHttp.Created(await handler.Handle(command, ct), c => $"/api/check-ins/{c.Id}"));

        return api;
    }
}
