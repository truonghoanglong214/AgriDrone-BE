using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Http;

namespace AgriDrone.SharedInfrastructure.Authentication;

internal sealed class CurrentTenant(
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext)
    : ICurrentTenant
{
    public Guid? TenantId =>
        GetGuidClaim(AgriDroneClaimTypes.TenantId) ?? executionContext.TenantId;

    public Guid? MembershipId => GetGuidClaim(
        AgriDroneClaimTypes.TenantMembershipId);

    public string? Role => httpContextAccessor.HttpContext?.User
        .FindFirst(AgriDroneClaimTypes.TenantRole)?
        .Value;

    public bool HasTenantContext => TenantId.HasValue;

    private Guid? GetGuidClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User
            .FindFirst(claimType)?
            .Value;

        return Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : null;
    }
}
