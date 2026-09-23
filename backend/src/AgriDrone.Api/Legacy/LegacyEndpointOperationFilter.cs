using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Globalization;

namespace AgriDrone.Api.Legacy;

public sealed class LegacyEndpointOperationFilter : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var legacyEndpoint = context.MethodInfo
            .GetCustomAttributes(inherit: true)
            .OfType<LegacyEndpointAttribute>()
            .SingleOrDefault();

        if (legacyEndpoint is null)
        {
            return;
        }

        operation.Deprecated = true;
        operation.Description = string.Join(
            Environment.NewLine + Environment.NewLine,
            new[]
            {
                operation.Description,
                $"LEGACY: blocked by default with 410 Gone. {legacyEndpoint.Replacement}"
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        operation.Responses ??= [];
        operation.Responses.TryAdd(
            StatusCodes.Status410Gone.ToString(
                CultureInfo.InvariantCulture),
            new OpenApiResponse
            {
                Description =
                    $"Legacy flow disabled ({LegacyEndpointGateMiddleware.DisabledErrorCode})."
            });
    }
}
