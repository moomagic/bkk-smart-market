using SmartMarket.Api.Auth;
using SmartMarket.Application.Reviews;

namespace SmartMarket.Api.Endpoints;

public static class ReviewsEndpoints
{
    public static IEndpointRouteBuilder MapReviews(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/reviews").RequireAuthorization(DemoPolicies.Officer);

        group.MapPost("/{id:guid}/respond", async (Guid id, RespondReview command, RespondReviewHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(id, command, ct)));

        return api;
    }
}
