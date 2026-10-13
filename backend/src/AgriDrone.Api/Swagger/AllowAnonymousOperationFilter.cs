using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AgriDrone.Api.Swagger;

internal sealed class AllowAnonymousOperationFilter : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var allowsAnonymous = context.MethodInfo
            .GetCustomAttributes(inherit: true)
            .OfType<IAllowAnonymous>()
            .Any() ||
            context.MethodInfo.DeclaringType?
                .GetCustomAttributes(inherit: true)
                .OfType<IAllowAnonymous>()
                .Any() == true;

        if (allowsAnonymous)
        {
            operation.Security = [];
        }
    }
}
