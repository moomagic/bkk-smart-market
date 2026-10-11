using SmartMarket.Api.Auth;
using SmartMarket.Application.Common;

namespace SmartMarket.Api.Endpoints;

/// <summary>Presenter tools, mapped in Development only. Moving the clock changes no data, so it writes no audit entry.</summary>
public static class DemoEndpoints
{
    public sealed record ClockDto(DateTimeOffset Now);

    public static IEndpointRouteBuilder MapDemo(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/demo").RequireAuthorization(DemoPolicies.Officer);

        group.MapGet("/clock", (IDemoClock clock) => new ClockDto(clock.Now));

        group.MapPost("/clock", (ClockDto body, IDemoClock clock) =>
        {
            clock.Set(body.Now);
            return new ClockDto(clock.Now);
        });

        return api;
    }
}
