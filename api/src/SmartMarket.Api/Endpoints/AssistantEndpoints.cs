using SmartMarket.Application.Assistant;

namespace SmartMarket.Api.Endpoints;

public static class AssistantEndpoints
{
    public static IEndpointRouteBuilder MapAssistant(this IEndpointRouteBuilder api)
    {
        // Any signed-in role: vendors ask; officers use the same answers when vendors call the district office.
        var group = api.MapGroup("/assistant").RequireAuthorization();

        group.MapPost("/ask", async (AskAssistant command, AskAssistantHandler handler, CancellationToken ct) =>
            ResultHttp.Ok(await handler.Handle(command, ct)));

        return api;
    }
}
