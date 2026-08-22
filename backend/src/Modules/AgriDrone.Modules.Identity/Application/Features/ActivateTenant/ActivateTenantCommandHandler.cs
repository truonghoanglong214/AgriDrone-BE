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

namespace AgriDrone.Modules.Identity.Application.Features.ActivateTenant
{
    internal sealed class ActivateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IIdentityUnitOfWork unitOfWork,
        IAuditWriter auditWriter,
        IAuditLogSink auditLogSink,
        IExecutionContext executionContext,
        TimeProvider timeProvider) : IRequestHandler<ActivateTenantCommand, Result>
    {
        public async Task<Result> Handle(ActivateTenantCommand request, CancellationToken cancellationToken)
        {
            var existedTenant = await tenantRepository.GetByIdIgnoreStatusAsync(request.TenantId, cancellationToken);
            if (existedTenant is null)
                return Result.Failure(UserError.TenantNotFound());

            if (existedTenant.Status == GeneralStatus.Active)
                return Result.Success();

            if (executionContext.ActorId is not Guid actorId)
                return Result.Failure(UserError.CurrentUserIsRequired());

            var now = timeProvider.GetUtcNow();
            var oldStatus = existedTenant.Status;

            existedTenant.Activate(now);

            using var oldData = JsonSerializer.SerializeToDocument(new
            {
                Status = oldStatus.ToString()
            });

            using var newData = JsonSerializer.SerializeToDocument(new
            {
                Status = existedTenant.Status.ToString()
            });

            auditWriter.AddSystemAdminAction(
                sink: auditLogSink,
                actorId: actorId,
                correlationId: executionContext.CorrelationId,
                entityType: "Tenant",
                entityId: existedTenant.Id,
                action: "ACTIVATE",
                oldData: oldData,
                newData: newData,
                createdAt: now);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
