using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Api.Surveys;

internal static class SurveyRequestHttpIdempotency
{
    public const string HeaderName = "Idempotency-Key";
    public const int MaximumKeyLength =
        RequestIdempotency.MaximumKeyLength;

    public static bool TryNormalize(
        HttpContext httpContext,
        string? value,
        out string normalized,
        out IResult? error)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is > 0 and <= MaximumKeyLength)
        {
            error = null;
            return true;
        }

        error = Results.ValidationProblem(
            new Dictionary<string, string[]>
            {
                [HeaderName] =
                [
                    $"{HeaderName} is required and cannot exceed {MaximumKeyLength} characters."
                ]
            },
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation error",
            detail: "A valid idempotency header is required.",
            instance: httpContext.Request.Path.ToString(),
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = "validation_error",
                ["traceId"] = httpContext.TraceIdentifier
            });
        return false;
    }
}
