using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AgriDrone.Modules.Identity.Application.Features.DeactivateTenant
{
    internal sealed class DeactivateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IIdentityUnitOfWork unitOfWork,
        IAuditWriter auditWriter,
        IAuditLogSink auditLogSink,
        IExecutionContext executionContext,
        TimeProvider timeProvider) : IRequestHandler<DeactivateTenantCommand, Result>
    {
        public async Task<Result> Handle(DeactivateTenantCommand request, CancellationToken cancellationToken)
        {
            var tenant = await tenantRepository.GetByIdIgnoreStatusAsync(request.TenantId, cancellationToken);
            if (tenant is null)
                return Result.Failure(TenantError.NotFound());

            if (tenant.Status == GeneralStatus.Inactive)
            {
                return Result.Success();
            }

            if (executionContext.ActorId is not Guid actorId)
            {
                return Result.Failure(AuthenticationError.CurrentUserRequired());
            }

            var now = timeProvider.GetUtcNow();
            var oldStatus = tenant.Status;

            tenant.Deactivate(now);

            using var oldData = JsonSerializer.SerializeToDocument(new
            {
                Status = oldStatus.ToString()
            });

            using var newData = JsonSerializer.SerializeToDocument(new
            {
                Status = tenant.Status.ToString()
            });

            auditWriter.AddSystemAdminAction(
                sink: auditLogSink,
                actorId: actorId,
                correlationId: executionContext.CorrelationId,
                entityType: "Tenant",
                entityId: tenant.Id,
                action: "DEACTIVATE",
                oldData: oldData,
                newData: newData,
                createdAt: now);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();

        }
    }
}
