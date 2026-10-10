using SmartMarket.Application.Common;

namespace SmartMarket.Api.Endpoints;

/// <summary>Maps business errors to ProblemDetails. The Thai message goes in "detail" for the UI.</summary>
public static class ResultHttp
{
    public static IResult Problem(Error error) => Results.Problem(
        statusCode: error.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        },
        title: error.Code,
        detail: error.Message,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
