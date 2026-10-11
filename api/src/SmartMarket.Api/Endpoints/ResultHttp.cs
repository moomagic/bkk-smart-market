using SmartMarket.Application.Common;

namespace SmartMarket.Api.Endpoints;

/// <summary>Maps business errors to ProblemDetails. The Thai message goes in "detail" for the UI.</summary>
public static class ResultHttp
{
    public static IResult Ok<T>(Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error!);

    public static IResult Created<T>(Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Results.Created(location(result.Value), result.Value) : Problem(result.Error!);

    public static IResult Problem(Error error) => Results.Problem(
        statusCode: error.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Invalid => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status403Forbidden,
        },
        title: error.Code,
        detail: error.Message,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
