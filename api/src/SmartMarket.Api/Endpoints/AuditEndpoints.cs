using SmartMarket.Api.Auth;
using SmartMarket.Application.Audit;

namespace SmartMarket.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAudit(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/audit").RequireAuthorization(DemoPolicies.Officer);

        group.MapGet("", async (string? entityType, string? entityId, GetAuditTrailHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(entityType, entityId, ct)));

        return api;
    }
}
