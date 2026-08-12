using AgriDrone.SharedKernel.Application;
using Microsoft.AspNetCore.Http;

namespace AgriDrone.SharedInfrastructure.Http;

public static class ResultMapper
{
    public static IResult ToHttpResult(
        this Result result,
        HttpContext httpContext,
        Func<IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess
            ? onSuccess()
            : ToProblem(result.Error, httpContext);
    }

    public static IResult ToHttpResult<T>(
        this Result<T> result,
        HttpContext httpContext,
        Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess
            ? onSuccess(result.Value)
            : ToProblem(result.Error, httpContext);
    }

    private static IResult ToProblem(
        AppError error,
        HttpContext httpContext)
    {
        var (statusCode, title) = error.Type switch
        {
            ErrorType.Failure =>
                (StatusCodes.Status400BadRequest, "Request failed"),

            ErrorType.Validation =>
                (StatusCodes.Status400BadRequest, "Validation error"),

            ErrorType.NotFound =>
                (StatusCodes.Status404NotFound, "Resource not found"),

            ErrorType.Conflict =>
                (StatusCodes.Status409Conflict, "Conflict"),

            ErrorType.Unauthorized =>
                (StatusCodes.Status401Unauthorized, "Unauthorized"),

            ErrorType.Forbidden =>
                (StatusCodes.Status403Forbidden, "Forbidden"),

            _ => throw new ArgumentOutOfRangeException(
                nameof(error),
                error.Type,
                "Unsupported application error type.")
        };

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: error.Description,
            instance: httpContext.Request.Path.ToString(),
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = error.Code,
                ["traceId"] = httpContext.TraceIdentifier
            });
    }
}
