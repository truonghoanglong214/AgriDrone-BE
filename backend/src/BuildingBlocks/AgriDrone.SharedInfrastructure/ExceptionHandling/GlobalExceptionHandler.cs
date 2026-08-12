using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgriDrone.SharedInfrastructure.ExceptionHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly Action<ILogger, Exception?> LogUnhandledException =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, nameof(LogUnhandledException)),
            "An unhandled exception occurred");

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(_logger, exception);

        var problemDetails = Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal server error",
            detail: "An unexpected error occurred.",
            instance: httpContext.Request.Path.ToString(),
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = "internal_server_error",
                ["traceId"] = httpContext.TraceIdentifier
            });

        await problemDetails.ExecuteAsync(httpContext);

        return true;
    }
}
