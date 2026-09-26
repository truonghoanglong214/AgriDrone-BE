using AgriDrone.Modules.Identity.Application.Provisioning;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.CreateTenant
{
    internal sealed class CreateTenantCommandHandler(
        ITenantProvisioningPort provisioningPort) : IRequestHandler<CreateTenantCommand, Result<CreateTenantResponse>>
    {
        public async Task<Result<CreateTenantResponse>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
        {
            var result = await provisioningPort.ProvisionAsync(
                new ProvisionTenantRequest(request.Code, request.Name),
                cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure<CreateTenantResponse>(result.Error);
            }

            return Result.Success(
                new CreateTenantResponse(
                    result.Value.TenantId,
                    result.Value.Code,
                    result.Value.Name,
                    result.Value.Status,
                    result.Value.CreatedAt));
        }
    }
}
