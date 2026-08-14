using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal static class AuthorizationRouteValues
{
    public static bool TryGetGuid(
        AuthorizationHandlerContext context,
        string routeKey,
        out Guid value,
        out CancellationToken cancellationToken)
    {
        if (context.Resource is HttpContext httpContext &&
            Guid.TryParse(
                httpContext.Request.RouteValues[routeKey]?.ToString(),
                out value))
        {
            cancellationToken = httpContext.RequestAborted;
            return true;
        }

        value = Guid.Empty;
        cancellationToken = CancellationToken.None;
        return false;
    }
}
