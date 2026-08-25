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

namespace AgriDrone.Modules.Identity.Application.Features.DeactivateTenantMembership
{
    internal sealed class DeactivateTenantMembershipHandler(
        ITenantMembershipRepository tenantMembershipRepository,
        IIdentityUnitOfWork unitOfWork,
        IAuditWriter auditWriter,
        IAuditLogSink auditLogSink,
        IExecutionContext executionContext,
        TimeProvider timeProvider) : IRequestHandler<DeactivateTenantMembershipCommand, Result>
    {
        public async Task<Result> Handle(DeactivateTenantMembershipCommand request, CancellationToken cancellationToken)
        {
            var tenantMembership = await tenantMembershipRepository.GetByIdAsync(request.tenantId, cancellationToken);
            if (tenantMembership is null)
                return Result.Failure(TenantMembershipError.NotFound());

            if (tenantMembership.Status == GeneralStatus.Inactive)
                return Result.Success();

            if (executionContext.ActorId is not Guid actorId)
                return Result.Failure(AuthenticationError.CurrentUserRequired());

            var now = timeProvider.GetUtcNow();
            var oldStatus = tenantMembership.Status;

            tenantMembership.Deactivate(now);

            using var oldData = JsonSerializer.SerializeToDocument(new
            {
                Status = oldStatus.ToString()
            });

            using var newData = JsonSerializer.SerializeToDocument(new
            {
                Status = tenantMembership.Status.ToString()
            });

            auditWriter.AddSystemAdminAction(
                sink: auditLogSink,
                actorId: actorId,
                correlationId: executionContext.CorrelationId,
                entityType: "TenantMembership",
                entityId: tenantMembership.Id,
                action: "DEACTIVATE",
                oldData: oldData,
                newData: newData,
                createdAt: now);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
