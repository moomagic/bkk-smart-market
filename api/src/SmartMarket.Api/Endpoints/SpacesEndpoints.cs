using SmartMarket.Application.Spaces;
using SmartMarket.Domain.Spaces;

namespace SmartMarket.Api.Endpoints;

public static class SpacesEndpoints
{
    public static IEndpointRouteBuilder MapSpaces(this IEndpointRouteBuilder api)
    {
        api.MapGet("/spaces", (Guid? districtId, SpaceType? type, ListSpacesHandler handler, CancellationToken ct) =>
            handler.Handle(districtId, type, ct));
        return api;
    }
}
