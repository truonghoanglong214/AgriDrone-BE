using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;

/// <summary>
/// Stages request-kind-specific onboarding resources in the specialized approval
/// DbContext. Implementations must not open, commit, or save through another
/// DbContext. The owning approval unit of work performs the only commit.
/// </summary>
public interface ISurveyApprovalProvisioningPort
{
    Task<Result<SurveyApprovalProvisionedTenant>> StageTenantAsync(
        StageSurveyApprovalTenant request,
        CancellationToken cancellationToken = default);

    Task<Result<SurveyApprovalProvisionedFarm>> StageFarmAsync(
        StageSurveyApprovalFarm request,
        CancellationToken cancellationToken = default);

    Task<Result<SurveyApprovalPrimaryAssignment>>
        StagePrimaryManagerAssignmentAsync(
            StageSurveyApprovalPrimaryAssignment request,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages the Owner invitation and its email Outbox message. The plaintext
    /// invitation token must exist only in the Outbox payload and is never returned.
    /// </summary>
    Task<Result<SurveyApprovalOwnerInvitation>> StageOwnerInvitationAsync(
        StageSurveyApprovalOwnerInvitation request,
        CancellationToken cancellationToken = default);
}

public sealed record StageSurveyApprovalTenant(
    string Code,
    string Name,
    DateTimeOffset CreatedAt);

public sealed record SurveyApprovalProvisionedTenant(
    Guid TenantId,
    string Code,
    string Name);

public sealed record StageSurveyApprovalFarm(
    Guid TenantId,
    Guid CreatedBy,
    string Code,
    string Name,
    string Address,
    decimal ApproximateAreaHa,
    double Longitude,
    double Latitude,
    int MapSrid,
    DateTimeOffset CreatedAt);

public sealed record SurveyApprovalProvisionedFarm(
    Guid FarmId,
    Guid TenantId,
    string Code,
    string Name);

public sealed record StageSurveyApprovalPrimaryAssignment(
    Guid TenantId,
    Guid FarmId,
    Guid SystemManagerProfileId,
    Guid AssignedBy,
    string Reason,
    DateTimeOffset AssignedAt);

public sealed record SurveyApprovalPrimaryAssignment(
    Guid AssignmentId,
    Guid SystemManagerProfileId);

public sealed record StageSurveyApprovalOwnerInvitation(
    Guid TenantId,
    Guid InvitedBy,
    string Email,
    DateTimeOffset CreatedAt);

public sealed record SurveyApprovalOwnerInvitation(
    Guid InvitationId,
    string Email,
    DateTimeOffset ExpiresAt);
