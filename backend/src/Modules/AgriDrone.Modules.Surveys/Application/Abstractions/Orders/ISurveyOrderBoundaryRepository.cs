using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Orders;

public interface ISurveyOrderBoundaryRepository
{
    Task<Result<SurveyOrderBoundaryVerificationState>>
        GetForVerificationAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset verifiedAt,
            CancellationToken cancellationToken = default);

    Task<Result<SurveyBoundaryApprovalSnapshot>> StageApprovedBoundaryAsync(
        SurveyOrder order,
        Polygon geometry,
        int? expectedCurrentBoundaryVersionNumber,
        Guid actorUserId,
        string reason,
        string? evidenceReference,
        DateTimeOffset verifiedAt,
        CancellationToken cancellationToken = default);
}

public sealed record SurveyOrderBoundaryVerificationState(
    SurveyOrder Order,
    Guid SystemManagerProfileId,
    Guid? CurrentPublishedBaseMapVersionId,
    int? CurrentConfirmedPoleCount);

public sealed record SurveyBoundaryApprovalSnapshot(
    Guid FarmBoundaryId,
    int VersionNumber);
