using System.Text.Json;
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateTenantRole;

internal sealed class UpdateTenantRoleCommandHandler(
    ITenantMembershipRepository tenantMembershipRepository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateTenantRoleCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTenantRoleCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure(
                AuthenticationError.CurrentUserRequired());
        }

        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure(
                TenantError.ContextRequired());
        }

        var accessDecision = await effectiveAccessService.CheckTenantAsync(
            actorId,
            tenantId,
            TenantAccessLevel.Owner,
            cancellationToken);

        if (!accessDecision.IsAllowed)
        {
            return Result.Failure(TenantError.AccessDenied());
        }

        if (actorId == request.UserId)
        {
            return Result.Failure(
                TenantMembershipError.SelfRoleChangeForbidden());
        }

        var membership = await tenantMembershipRepository
            .GetByUserAndTenantIdAsync(
                request.UserId,
                tenantId,
                cancellationToken);

        if (membership is null)
        {
            return Result.Failure(TenantMembershipError.NotFound());
        }

        if (membership.Status != GeneralStatus.Active)
        {
            return Result.Failure(TenantMembershipError.Inactive());
        }

        if (membership.User.Status != UserStatus.Active ||
            membership.User.DeletedAt is not null)
        {
            return Result.Failure(
                TenantMembershipError.TargetUserInactive());
        }

        if (membership.Role == TenantMemberRole.Owner ||
            request.Role == TenantMemberRole.Owner)
        {
            return Result.Failure(
                TenantMembershipError.OwnerRoleProtected());
        }

        if (membership.Role == request.Role)
        {
            return Result.Success();
        }

        var oldRole = membership.Role;
        var now = timeProvider.GetUtcNow();

        membership.ChangeRole(request.Role);

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            Role = oldRole.ToString()
        });

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            Role = membership.Role.ToString()
        });

        auditWriter.AddUserAction(
            sink: auditLogSink,
            tenantId: tenantId,
            farmId: null,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: "TenantMembership",
            entityId: membership.Id,
            action: "UPDATE_ROLE",
            oldData: oldData,
            newData: newData,
            createdAt: now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(
                TenantMembershipError.ConcurrentUpdate());
        }

        return Result.Success();
    }
}
