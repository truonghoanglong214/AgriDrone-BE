using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Application.Features.CreateTenant
{
    internal sealed class CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IIdentityUnitOfWork unitOfWork) : IRequestHandler<CreateTenantCommand, Result<CreateTenantResponse>>
    {
        private const string ActiveTenantCodeConstraint =
            "ux_tenants_code_active";

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

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.IsUniqueConstraintViolation(
                    ActiveTenantCodeConstraint))
            {
                return Result.Failure<CreateTenantResponse>(
                    TenantError.CodeAlreadyExists(code));
            }

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
