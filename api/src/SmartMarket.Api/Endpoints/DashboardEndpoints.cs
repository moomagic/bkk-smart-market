using SmartMarket.Api.Auth;
using SmartMarket.Application.Reporting;

namespace SmartMarket.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/dashboard").RequireAuthorization(DemoPolicies.Officer);

        group.MapGet("/kpis", (Guid? districtId, GetDashboardHandler handler, CancellationToken ct) => handler.Handle(districtId, ct));

        // POST: made on request (it may call a model), never cached; it changes nothing
        group.MapPost("/summary", (Guid? districtId, SummarizeDashboardHandler handler, CancellationToken ct) => handler.Handle(districtId, ct));

        return api;
    }
}
