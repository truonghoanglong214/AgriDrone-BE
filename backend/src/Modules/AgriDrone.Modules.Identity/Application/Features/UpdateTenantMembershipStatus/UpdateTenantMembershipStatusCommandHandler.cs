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

namespace AgriDrone.Modules.Identity.Application.Features.UpdateTenantMembershipStatus;

internal sealed class UpdateTenantMembershipStatusCommandHandler(
    ITenantMembershipRepository tenantMembershipRepository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateTenantMembershipStatusCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTenantMembershipStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure(
                AuthenticationError.CurrentUserRequired());
        }

        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure(TenantError.ContextRequired());
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

        var membership = await tenantMembershipRepository
            .GetByUserAndTenantIdAsync(
                request.UserId,
                tenantId,
                cancellationToken);

        if (membership is null)
        {
            return Result.Failure(TenantMembershipError.NotFound());
        }

        if (membership.Status == request.Status)
        {
            return Result.Success();
        }

        if (membership.User.Status != UserStatus.Active ||
            membership.User.DeletedAt is not null)
        {
            return Result.Failure(
                TenantMembershipError.TargetUserInactive());
        }

        if (membership.Role == TenantMemberRole.Owner &&
            request.Status == GeneralStatus.Inactive)
        {
            return Result.Failure(
                TenantMembershipError.OwnerMembershipProtected());
        }

        var oldStatus = membership.Status;
        var now = timeProvider.GetUtcNow();

        if (request.Status == GeneralStatus.Active)
        {
            membership.Activate(now);
        }
        else
        {
            membership.Deactivate(now);
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            Status = oldStatus.ToString()
        });

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            Status = membership.Status.ToString()
        });

        auditWriter.AddUserAction(
            sink: auditLogSink,
            tenantId: tenantId,
            farmId: null,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: "TenantMembership",
            entityId: membership.Id,
            action: request.Status == GeneralStatus.Active
                ? "ACTIVATE"
                : "DEACTIVATE",
            oldData: oldData,
            newData: newData,
            createdAt: now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
