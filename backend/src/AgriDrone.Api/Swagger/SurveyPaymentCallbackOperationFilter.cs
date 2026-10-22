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

        operation.Parameters ??= [];
        var authorizationParameter = operation.Parameters
            .OfType<OpenApiParameter>()
            .FirstOrDefault(parameter => parameter.In == ParameterLocation.Header &&
                parameter.Name == "Authorization");
        if (authorizationParameter is not null)
        {
            authorizationParameter.Required = false;
            authorizationParameter.Description =
                "For SePay, use `Apikey <API_KEY>`. Other configured providers may require the timestamp and signature headers.";
        }
    }
}
