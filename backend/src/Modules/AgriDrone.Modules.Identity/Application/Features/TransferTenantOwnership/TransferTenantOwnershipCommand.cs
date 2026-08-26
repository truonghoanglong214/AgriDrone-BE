using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.TransferTenantOwnership
{
    public sealed record TransferTenantOwnershipCommand(
        Guid NewOwnerUserId) : IRequest<Result>;
}
