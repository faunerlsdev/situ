using Situ.Application.Common.Results;

namespace Situ.Api.Http;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error!);

    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error!);

    private static IResult Problem(Error error)
    {
        var status = StatusCode(error.Type);
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        foreach (var (key, value) in error.Extensions)
        {
            extensions[key] = value is DateTimeOffset moment ? moment.ToUniversalTime().ToString("O") : value;
        }

        return Results.Problem(
            title: Title(error.Type),
            detail: error.Message,
            statusCode: status,
            extensions: extensions);
    }

    private static int StatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Locked => StatusCodes.Status423Locked,
        ErrorType.Failure => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string Title(ErrorType type) => type switch
    {
        ErrorType.Validation => "Validation failed",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "Conflict",
        ErrorType.Locked => "Locked",
        ErrorType.Failure => "Failure",
        _ => "Failure",
    };
}
