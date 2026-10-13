using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyApprovalProvisioningPort(
    SurveyApprovalDbContext context,
    IInvitationTokenService invitationTokenService,
    IExecutionContext executionContext,
    IOptions<TenantInvitationOptions> invitationOptions,
    OutboxMessageFactory outboxMessageFactory)
    : ISurveyApprovalProvisioningPort
{
    private readonly TenantInvitationOptions _invitationOptions =
        invitationOptions.Value;

    public async Task<Result<SurveyApprovalProvisionedTenant>> StageTenantAsync(
        StageSurveyApprovalTenant request,
        CancellationToken cancellationToken = default)
    {
        context.EnsureApprovalTransaction();
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();

        if (code.Length is 0 or > 50 || name.Length is 0 or > 150 ||
            await context.Tenants.AnyAsync(
                tenant => tenant.Code == code && tenant.DeletedAt == null,
                cancellationToken))
        {
            return Result.Failure<SurveyApprovalProvisionedTenant>(
                SurveyApprovalError.TenantProvisioningConflict());
        }

        var tenant = Tenant.Create(
            code,
            name,
            GeneralStatus.Active,
            request.CreatedAt);
        context.Tenants.Add(tenant);

        await context.FlushCheckpointAsync(
            SurveyApprovalPersistenceCheckpoint.Tenant,
            cancellationToken);

        return Result.Success(new SurveyApprovalProvisionedTenant(
            tenant.Id,
            tenant.Code,
            tenant.Name));
    }

    public async Task<Result<SurveyApprovalProvisionedFarm>> StageFarmAsync(
        StageSurveyApprovalFarm request,
        CancellationToken cancellationToken = default)
    {
        context.EnsureApprovalTransaction();
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();

        if (request.MapSrid != 4326 ||
            !double.IsFinite(request.Longitude) ||
            !double.IsFinite(request.Latitude) ||
            request.Longitude is < -180d or > 180d ||
            request.Latitude is < -90d or > 90d ||
            request.ApproximateAreaHa <= 0 ||
            code.Length is 0 or > 30 ||
            name.Length is 0 or > 150 ||
            !await context.Tenants.AnyAsync(
                tenant => tenant.Id == request.TenantId &&
                          tenant.Status == GeneralStatus.Active &&
                          tenant.DeletedAt == null,
                cancellationToken) ||
            await context.Farms.AnyAsync(
                farm => farm.TenantId == request.TenantId &&
                        farm.Code == code &&
                        farm.DeletedAt == null,
                cancellationToken))
        {
            return Result.Failure<SurveyApprovalProvisionedFarm>(
                SurveyApprovalError.FarmProvisioningConflict());
        }

        var centerPoint = new Point(request.Longitude, request.Latitude)
        {
            SRID = request.MapSrid
        };
        var farm = Farm.Create(
            request.TenantId,
            code,
            name,
            request.Address.Trim(),
            boundary: null,
            centerPoint,
            request.ApproximateAreaHa,
            GeneralStatus.Active,
            request.CreatedBy,
            request.CreatedAt);
        context.Farms.Add(farm);

        await context.FlushCheckpointAsync(
            SurveyApprovalPersistenceCheckpoint.Farm,
            cancellationToken);

        return Result.Success(new SurveyApprovalProvisionedFarm(
            farm.Id,
            farm.TenantId,
            farm.Code,
            farm.Name));
    }

    public async Task<Result<SurveyApprovalPrimaryAssignment>>
        StagePrimaryManagerAssignmentAsync(
            StageSurveyApprovalPrimaryAssignment request,
            CancellationToken cancellationToken = default)
    {
        context.EnsureApprovalTransaction();

        if (await context.FarmManagerAssignments.AnyAsync(
                assignment => assignment.FarmId == request.FarmId &&
                              assignment.EndedAt == null,
                cancellationToken))
        {
            return Result.Failure<SurveyApprovalPrimaryAssignment>(
                SurveyApprovalError.PrimaryAssignmentConflict());
        }

        var assignment = FarmManagerAssignment.Create(
            request.TenantId,
            request.FarmId,
            request.SystemManagerProfileId,
            request.AssignedBy,
            request.Reason,
            request.AssignedAt);
        context.FarmManagerAssignments.Add(assignment);

        await context.FlushCheckpointAsync(
            SurveyApprovalPersistenceCheckpoint.PrimaryManagerAssignment,
            cancellationToken);

        return Result.Success(new SurveyApprovalPrimaryAssignment(
            assignment.Id,
            assignment.SystemManagerProfileId));
    }

    public async Task<Result<SurveyApprovalOwnerInvitation>>
        StageOwnerInvitationAsync(
            StageSurveyApprovalOwnerInvitation request,
            CancellationToken cancellationToken = default)
    {
        context.EnsureApprovalTransaction();
        var email = request.Email.Trim().ToLowerInvariant();

        var hasOwner = await context.TenantMemberships.AnyAsync(
            membership => membership.TenantId == request.TenantId &&
                          membership.Role == TenantMemberRole.Owner &&
                          membership.Status == GeneralStatus.Active,
            cancellationToken);
        var hasPendingOwnerInvitation = await context.TenantInvitations.AnyAsync(
            invitation => invitation.TenantId == request.TenantId &&
                          invitation.Purpose ==
                              TenantInvitationPurpose.OwnerProvisioning &&
                          invitation.Status == TenantInvitationStatus.Pending,
            cancellationToken);
        var existingUser = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == email, cancellationToken);
        var isExistingMember = existingUser is not null &&
            await context.TenantMemberships.AnyAsync(
                membership => membership.TenantId == request.TenantId &&
                              membership.UserId == existingUser.Id,
                cancellationToken);

        if (email.Length is 0 or > 320 ||
            hasOwner ||
            hasPendingOwnerInvitation ||
            existingUser?.Id == request.InvitedBy ||
            isExistingMember)
        {
            return Result.Failure<SurveyApprovalOwnerInvitation>(
                SurveyApprovalError.OwnerInvitationConflict());
        }

        var token = invitationTokenService.Generate();
        var expiresAt = request.CreatedAt.AddHours(
            _invitationOptions.ExpirationHours);
        var invitation = TenantInvitation.Create(
            request.TenantId,
            email,
            TenantMemberRole.Owner,
            TenantInvitationPurpose.OwnerProvisioning,
            token.TokenHash,
            request.InvitedBy,
            expiresAt,
            request.CreatedAt);
        context.TenantInvitations.Add(invitation);

        await context.FlushCheckpointAsync(
            SurveyApprovalPersistenceCheckpoint.OwnerInvitation,
            cancellationToken);

        var payload = new TenantInvitationEmailRequestedV1(
            invitation.Id,
            token.PlainTextToken);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.TenantInvitationEmailRequestedV1,
            messageId: Guid.NewGuid(),
            correlationId: executionContext.CorrelationId,
            tenantId: request.TenantId,
            actorId: request.InvitedBy,
            occurredAt: request.CreatedAt,
            payload);
        context.AddOutboxMessage(outboxMessageFactory.Create(
            envelope,
            envelope.EventType,
            invitation.Id.ToString("D")));

        return Result.Success(new SurveyApprovalOwnerInvitation(
            invitation.Id,
            invitation.Email,
            invitation.ExpiresAt));
    }
}
