using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.CreateTenant
{
    internal sealed class CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IIdentityUnitOfWork unitOfWork) : IRequestHandler<CreateTenantCommand, Result<CreateTenantResponse>>
    {
        public async Task<Result<CreateTenantResponse>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim().ToUpperInvariant();
            var name = request.Name.Trim();
            var existedTenant = await tenantRepository.GetByCodeAsync(code, cancellationToken);
            if (existedTenant is not null)
                return Result.Failure<CreateTenantResponse>(TenantError.CodeAlreadyExists(code));

            var newTenant = Tenant.Create(
                code,
                name,
                GeneralStatus.Active,
                DateTimeOffset.UtcNow);

            tenantRepository.Add(newTenant);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(
                new CreateTenantResponse(
                    newTenant.Id,
                    newTenant.Code,
                    newTenant.Name,
                    newTenant.Status,
                    newTenant.CreatedAt));
        }
    }
}
