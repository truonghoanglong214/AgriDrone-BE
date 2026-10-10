using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

/// <summary>
/// Reads the authoritative cross-module references required by survey approval.
/// Every method must use the active <see cref="Persistence.ISurveyApprovalUnitOfWork"/>
/// connection and transaction. Every mutable row that influences the approval
/// decision must be protected from concurrent changes until that transaction
/// completes; independent module DbContexts and eventually-consistent replicas
/// are not valid implementations.
/// </summary>
public interface ISurveyApprovalReferenceQueries
{
    Task<SurveyApprovalServiceReference?> GetServiceForApprovalAsync(
        Guid surveyServiceId,
        CancellationToken cancellationToken = default);

    Task<SurveyApprovalTenantOwnerReference?> GetTenantOwnerForApprovalAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SurveyApprovalFarmReference?> GetFarmForApprovalAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);

    Task<SurveyApprovalManagerReference?> GetManagerForApprovalAsync(
        Guid systemManagerProfileId,
        CancellationToken cancellationToken = default);

    Task<SurveyApprovalPrimaryAssignmentReference?>
        GetActivePrimaryAssignmentForApprovalAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current published Farm base map and its authoritative confirmed
    /// active-pole count from that map's source SurveyOrder. Raw/candidate Plant
    /// counts and Mission detected counts are not authoritative. No published map
    /// is represented by both nullable fields being null; it is not a not-found
    /// error.
    /// </summary>
    Task<SurveyApprovalFarmBaselineReference> GetFarmBaselineForApprovalAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recently published compatible result whose order is
    /// Completed and belongs to the same Tenant, Farm and SurveyService. Ordering
    /// is PublishedAt descending, then OrderId descending.
    /// </summary>
    Task<SurveyApprovalPreviousOrderReference?>
        GetPreviousCompatibleOrderForApprovalAsync(
            Guid tenantId,
            Guid farmId,
            Guid surveyServiceId,
            CancellationToken cancellationToken = default);
}

public sealed record SurveyApprovalServiceReference(
    Guid SurveyServiceId,
    SurveyServiceType ServiceType,
    SurveyServiceStatus Status,
    uint Version);

public sealed record SurveyApprovalTenantOwnerReference(
    Guid TenantId,
    Guid UserId,
    bool IsOwner,
    bool IsMembershipActive,
    bool IsTenantActive,
    bool IsUserActive,
    uint MembershipVersion);

public sealed record SurveyApprovalFarmReference(
    Guid TenantId,
    Guid FarmId,
    bool IsActive,
    long Version);

public sealed record SurveyApprovalManagerReference(
    Guid SystemManagerProfileId,
    Guid UserId,
    bool IsUserActive,
    bool IsProfileActive,
    bool IsAvailable,
    bool IsFlightQualified,
    DateTimeOffset? QualificationExpiresAt,
    long Version);

public sealed record SurveyApprovalPrimaryAssignmentReference(
    Guid AssignmentId,
    Guid TenantId,
    Guid FarmId,
    Guid SystemManagerProfileId,
    long Version);

public sealed record SurveyApprovalFarmBaselineReference(
    Guid TenantId,
    Guid FarmId,
    Guid? CurrentPublishedBaseMapVersionId,
    int? ConfirmedActivePoleCount);

public sealed record SurveyApprovalPreviousOrderReference(
    Guid SurveyOrderId,
    DateTimeOffset PublishedAt);
