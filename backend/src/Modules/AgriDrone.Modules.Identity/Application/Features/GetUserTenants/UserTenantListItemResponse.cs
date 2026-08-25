using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Application.Features.GetUserTenants;

public sealed record UserTenantListItemResponse(
    Guid Id,
    Guid TenantId,
    TenantMemberRole Role,
    GeneralStatus Status,
    DateTimeOffset? JoinedAt,
    DateTimeOffset CreatedAt);
