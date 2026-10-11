using SmartMarket.Application.Spaces;
using SmartMarket.Domain.Spaces;

namespace SmartMarket.Api.Endpoints;

public static class SpacesEndpoints
{
    public static IEndpointRouteBuilder MapSpaces(this IEndpointRouteBuilder api)
    {
        api.MapGet("/spaces", (Guid? districtId, SpaceType? type, ListSpacesHandler handler, CancellationToken ct) =>
            handler.Handle(districtId, type, ct));

        api.MapPost("/spaces/match", async (MatchQuery query, Guid? districtId, MatchSpacesHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(query, districtId, ct)));
        return api;
    }
}
