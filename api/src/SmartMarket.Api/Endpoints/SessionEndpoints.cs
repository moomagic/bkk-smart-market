using SmartMarket.Application.Common;

namespace SmartMarket.Api.Endpoints;

public static class SessionEndpoints
{
    public sealed record SessionDto(string User, string Role);

    /// <summary>Echoes who the API thinks you are — lets the web role switcher confirm its headers.</summary>
    public static IEndpointRouteBuilder MapSession(this IEndpointRouteBuilder api)
    {
        api.MapGet("/session", (ICurrentActor actor) => new SessionDto(actor.Actor, actor.Role));
        return api;
    }
}
