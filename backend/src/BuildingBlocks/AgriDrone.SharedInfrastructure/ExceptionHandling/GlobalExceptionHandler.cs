using FluentValidation;
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

    private static readonly Action<ILogger, Exception?> LogValidationException =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, nameof(LogValidationException)),
            "Request validation failed");

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
        var problemDetails = exception switch
        {
            ValidationException validationException =>
                CreateValidationProblem(httpContext, validationException),

            _ => CreateInternalServerProblem(httpContext, exception)
        };

        await problemDetails.ExecuteAsync(httpContext);

        return true;
    }

    private IResult CreateValidationProblem(
        HttpContext httpContext,
        ValidationException exception)
    {
        LogValidationException(_logger, exception);

        var errors = exception.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(failure => failure.ErrorMessage)
                    .Distinct()
                    .ToArray());

        return Results.ValidationProblem(
            errors,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation error",
            detail: "One or more validation errors occurred.",
            instance: httpContext.Request.Path.ToString(),
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = "validation_error",
                ["traceId"] = httpContext.TraceIdentifier
            });
    }

    private IResult CreateInternalServerProblem(
        HttpContext httpContext,
        Exception exception)
    {
        LogUnhandledException(_logger, exception);

        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal server error",
            detail: "An unexpected error occurred.",
            instance: httpContext.Request.Path.ToString(),
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = "internal_server_error",
                ["traceId"] = httpContext.TraceIdentifier
            });
    }
}
