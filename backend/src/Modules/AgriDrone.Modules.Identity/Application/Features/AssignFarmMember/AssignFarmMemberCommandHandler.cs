using System.Text.Json;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Application.Features.AssignFarmMember;

internal sealed class AssignFarmMemberCommandHandler(
    ITenantMembershipRepository tenantMembershipRepository,
    IFarmMembershipRepository farmMembershipRepository,
    IFarmAssignmentReferenceQuery farmReferenceQuery,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<AssignFarmMemberCommand, Result<AssignFarmMemberResponse>>
{
    public async Task<Result<AssignFarmMemberResponse>> Handle(
        AssignFarmMemberCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                TenantError.ContextRequired());
        }

        var accessDecision = await effectiveAccessService.CheckTenantAsync(
            actorId,
            tenantId,
            TenantAccessLevel.Admin,
            cancellationToken);

        if (!accessDecision.IsAllowed)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                TenantError.AccessDenied());
        }

        if (!await farmReferenceQuery.IsActiveFarmAsync(
                tenantId,
                request.FarmId,
                cancellationToken))
        {
            return Result.Failure<AssignFarmMemberResponse>(
                FarmMembershipError.FarmNotFound());
        }

        var tenantMembership = await tenantMembershipRepository
            .GetByUserAndTenantIdAsync(
                request.UserId,
                tenantId,
                cancellationToken);

        if (tenantMembership is null)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                TenantMembershipError.NotFound());
        }

        if (tenantMembership.Status != GeneralStatus.Active)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                FarmMembershipError.TargetTenantMembershipInactive());
        }

        if (tenantMembership.User.Status != UserStatus.Active ||
            tenantMembership.User.DeletedAt is not null)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                TenantMembershipError.TargetUserInactive());
        }

        if (tenantMembership.Role != TenantMemberRole.TenantAdmin)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                FarmMembershipError.TargetMustBeTenantAdmin());
        }

        var assignment = await farmMembershipRepository
            .GetByFarmAndUserAsync(
                tenantId,
                request.FarmId,
                request.UserId,
                cancellationToken);

        if (assignment is not null &&
            assignment.Role == request.Role &&
            assignment.AccessScope == request.AccessScope &&
            assignment.Status == GeneralStatus.Active)
        {
            return Result.Success(ToResponse(assignment));
        }

        if (assignment is null && request.ExpectedVersion.HasValue)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                FarmMembershipError.ConcurrentUpdate());
        }

        if (assignment is not null && !request.ExpectedVersion.HasValue)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                FarmMembershipError.ExpectedVersionRequired());
        }

        if (assignment is not null &&
            assignment.Version != request.ExpectedVersion!.Value)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                FarmMembershipError.ConcurrentUpdate());
        }

        var now = timeProvider.GetUtcNow();

        using var oldData = assignment is null
            ? null
            : JsonSerializer.SerializeToDocument(new
            {
                Role = assignment.Role.ToString(),
                AccessScope = assignment.AccessScope.ToString(),
                Status = assignment.Status.ToString(),
                Version = assignment.Version
            });

        if (assignment is null)
        {
            assignment = FarmMembership.Create(
                tenantId,
                request.FarmId,
                request.UserId,
                request.Role,
                request.AccessScope,
                now);

            farmMembershipRepository.Add(assignment);
        }
        else
        {
            assignment.Assign(
                request.Role,
                request.AccessScope,
                now);
        }

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            Role = assignment.Role.ToString(),
            AccessScope = assignment.AccessScope.ToString(),
            Status = assignment.Status.ToString(),
            ZoneIds = request.ZoneIds,
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
            action: "ASSIGN_FARM_MEMBER",
            oldData: oldData,
            newData: newData,
            createdAt: now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                FarmMembershipError.ConcurrentUpdate());
        }
        catch (FarmMembershipAssignmentConflictException)
        {
            return Result.Failure<AssignFarmMemberResponse>(
                FarmMembershipError.ConcurrentUpdate());
        }

        return Result.Success(ToResponse(assignment));
    }

    private static AssignFarmMemberResponse ToResponse(
        FarmMembership assignment) =>
        new(
            assignment.Id,
            assignment.TenantId,
            assignment.FarmId,
            assignment.UserId,
            assignment.Role,
            assignment.AccessScope,
            assignment.Status,
            assignment.Version,
            assignment.JoinedAt);
}
