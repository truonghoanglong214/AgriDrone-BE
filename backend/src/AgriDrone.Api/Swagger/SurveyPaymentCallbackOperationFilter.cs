using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AgriDrone.Api.Swagger;

internal sealed class SurveyPaymentCallbackOperationFilter : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(SurveyPaymentCallbacksController))
        {
            return;
        }

        var schema = context.SchemaGenerator.GenerateSchema(
            typeof(SurveyPaymentCallbackRequest),
            context.SchemaRepository);
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = schema
                }
            }
        };

        foreach (var parameter in (operation.Parameters ?? [])
                     .OfType<OpenApiParameter>()
                     .Where(parameter =>
                         parameter.In == ParameterLocation.Header &&
                         parameter.Name?.StartsWith(
                             "X-AgriDrone-Payment-",
                             StringComparison.Ordinal) == true))
        {
            parameter.Required = true;
        }
    }
}
