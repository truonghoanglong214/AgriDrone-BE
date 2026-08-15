using System;
using System.Collections.Generic;
using System.Text;
using AgriDrone.Modules.Identity.Domain.Tenants;

namespace AgriDrone.Modules.Identity.Application.Features.LoginUser
{
    public sealed record LoginUserResponse(
        string Email,
        string FullName,
        string? Phone,
        AuthenticationSessionResponse? Session,
        TenantSelectionResponse? TenantSelection);

    public sealed record AuthenticationSessionResponse(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        TenantOptionResponse? Tenant);

    public sealed record TenantSelectionResponse(
        string SelectionToken,
        DateTimeOffset ExpiresAt,
        IReadOnlyCollection<TenantOptionResponse> Tenants);

    public sealed record TenantOptionResponse(
        Guid Id,
        string Code,
        string Name,
        TenantMemberRole Role);
}
