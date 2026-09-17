using System.Text.Json;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Application.Features.RevokeFarmMemberAssignment;

internal sealed class RevokeFarmMemberAssignmentCommandHandler(
    ITenantMembershipRepository tenantMembershipRepository,
    IFarmMembershipRepository farmMembershipRepository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<RevokeFarmMemberAssignmentCommand, Result>
{
    public async Task<Result> Handle(
        RevokeFarmMemberAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure(AuthenticationError.CurrentUserRequired());
        }

        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure(TenantError.ContextRequired());
        }

        var adminAccessDecision = await effectiveAccessService.CheckTenantAsync(
            actorId,
            tenantId,
            TenantAccessLevel.Admin,
            cancellationToken);

        if (!adminAccessDecision.IsAllowed)
        {
            return Result.Failure(TenantError.AccessDenied());
        }

        var assignment = await farmMembershipRepository.GetByFarmAndUserAsync(
            tenantId,
            request.FarmId,
            request.UserId,
            cancellationToken);

        if (assignment is null)
        {
            return Result.Failure(FarmMembershipError.NotFound());
        }

        var tenantMembership = await tenantMembershipRepository
            .GetByUserAndTenantIdAsync(
                request.UserId,
                tenantId,
                cancellationToken);

        if (tenantMembership is null)
        {
            return Result.Failure(TenantMembershipError.NotFound());
        }

        if (tenantMembership.Role is TenantMemberRole.TenantAdmin or
            TenantMemberRole.Owner)
        {
            var ownerAccessDecision =
                await effectiveAccessService.CheckTenantAsync(
                    actorId,
                    tenantId,
                    TenantAccessLevel.Owner,
                    cancellationToken);

            if (!ownerAccessDecision.IsAllowed)
            {
                return Result.Failure(TenantError.AccessDenied());
            }
        }
        else if (tenantMembership.Role != TenantMemberRole.Member)
        {
            return Result.Failure(
                FarmMembershipError.TargetTenantRoleNotAssignable());
        }

        if (assignment.Status == GeneralStatus.Inactive)
        {
            return Result.Success();
        }

        if (assignment.Version != request.ExpectedVersion)
        {
            return Result.Failure(FarmMembershipError.ConcurrentUpdate());
        }

        var now = timeProvider.GetUtcNow();

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            UserId = assignment.UserId,
            Role = assignment.Role.ToString(),
            AccessScope = assignment.AccessScope.ToString(),
            Status = assignment.Status.ToString(),
            Version = assignment.Version
        });

        assignment.Deactivate(now);

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            UserId = assignment.UserId,
            Role = assignment.Role.ToString(),
            AccessScope = assignment.AccessScope.ToString(),
            Status = assignment.Status.ToString(),
            Version = assignment.Version,
            request.Reason
        });

        auditWriter.AddUserAction(
            sink: auditLogSink,
            tenantId: tenantId,
            farmId: request.FarmId,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: "FarmMembership",
            entityId: assignment.Id,
            action: "REVOKE_FARM_MEMBER_ASSIGNMENT",
            oldData: oldData,
            newData: newData,
            createdAt: now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(FarmMembershipError.ConcurrentUpdate());
        }

        return Result.Success();
    }
}
