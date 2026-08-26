using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Invitations.Creation;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgriDrone.Modules.Identity.Application.Features.TransferTenantOwnership
{
    internal sealed class TransferTenantOwnershipCommandHandler(
        ITenantMembershipRepository tenantMembershipRepository,
        IIdentityUnitOfWork unitOfWork,
        IEffectiveAccessService effectiveAccessService,
        IAuditWriter auditWriter,
        IAuditLogSink auditLogSink,
        IExecutionContext executionContext,
        TimeProvider timeProvider) : IRequestHandler<TransferTenantOwnershipCommand, Result>
    {
        public async Task<Result> Handle(
            TransferTenantOwnershipCommand request,
            CancellationToken cancellationToken)
        {
            if (executionContext.ActorId is not Guid actorId)
                return Result.Failure(AuthenticationError.CurrentUserRequired());

            if (executionContext.TenantId is not Guid tenantId)
                return Result.Failure(TenantError.ContextRequired());

            if (actorId == request.NewOwnerUserId)
                return Result.Failure(TenantMembershipError.OwnershipTransferToSelf());

            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(
                    ct => TransferAsync(
                        actorId,
                        tenantId,
                        request.NewOwnerUserId,
                        ct),
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure(
                    TenantMembershipError.OwnershipChanged());
            }
            catch (ActiveTenantOwnerConflictException)
            {
                return Result.Failure(
                    TenantMembershipError.OwnershipChanged());
            }

        }

        private async Task<Result> TransferAsync(
            Guid actorId,
            Guid tenantId,
            Guid newOwnerUserId,
            CancellationToken cancellationToken)
        {
            var access = await effectiveAccessService.CheckTenantAsync(
                actorId,
                tenantId,
                TenantAccessLevel.Owner,
                cancellationToken);

            if (!access.IsAllowed)
                return Result.Failure(TenantError.AccessDenied());

            var currentOwner = await tenantMembershipRepository
                .GetActiveOwnerAsync(tenantId, cancellationToken);
            if (currentOwner is null || currentOwner.UserId != actorId)
                return Result.Failure(TenantMembershipError.OwnershipChanged());

            var newOwner = await tenantMembershipRepository
                .GetByUserAndTenantIdAsync(
                    newOwnerUserId,
                    tenantId,
                    cancellationToken);

            if (newOwner is null)
                return Result.Failure(TenantMembershipError.NewOwnerNotFound());

            if (newOwner.Status != GeneralStatus.Active)
                return Result.Failure(TenantMembershipError.NewOwnerInactive());

            if (newOwner.User.Status != UserStatus.Active || newOwner.User.DeletedAt is not null)
                return Result.Failure(TenantMembershipError.NewOwnerUserInactive());

            var oldNewOwnerRole = newOwner.Role;
            var now = timeProvider.GetUtcNow();

            currentOwner.RelinquishOwnership(TenantMemberRole.TenantAdmin);

            AddRoleAudit(
                currentOwner,
                actorId,
                tenantId,
                TenantMemberRole.Owner,
                TenantMemberRole.TenantAdmin,
                now);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            newOwner.AssumeOwnership();

            AddRoleAudit(
                newOwner,
                actorId,
                tenantId,
                oldNewOwnerRole,
                TenantMemberRole.Owner,
                now);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        private void AddRoleAudit(
            TenantMembership membership,
            Guid actorId,
            Guid tenantId,
            TenantMemberRole oldRole,
            TenantMemberRole newRole,
            DateTimeOffset createdAt)
        {
            using var oldData = JsonSerializer.SerializeToDocument(new
            {
                Role = oldRole.ToString()
            });

            using var newData = JsonSerializer.SerializeToDocument(new
            {
                Role = newRole.ToString()
            });

            auditWriter.AddUserAction(
                sink: auditLogSink,
                tenantId: tenantId,
                farmId: null,
                actorId: actorId,
                correlationId: executionContext.CorrelationId,
                entityType: "TenantMembership",
                entityId: membership.Id,
                action: "TRANSFER_OWNERSHIP",
                oldData: oldData,
                newData: newData,
                createdAt: createdAt);
        }
    }
}
