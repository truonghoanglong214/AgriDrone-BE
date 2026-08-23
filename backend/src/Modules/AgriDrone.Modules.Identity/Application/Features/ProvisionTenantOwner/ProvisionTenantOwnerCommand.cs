using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.ProvisionTenantOwner;

public sealed record ProvisionTenantOwnerCommand(
    Guid TenantId,
    string Email) : IRequest<Result<ProvisionTenantOwnerResponse>>;
