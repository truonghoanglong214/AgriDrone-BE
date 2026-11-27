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

    Task<Guid?> FindCaptureOrthophotoAsync(
        SurveyOrder order, Guid attemptId, uint missionVersion, CancellationToken cancellationToken) =>
        Task.FromResult<Guid?>(null);

    Task<uint?> BindCaptureBoundaryAsync(
        SurveyOrder order, Guid attemptId, uint missionVersion, Guid boundaryId,
        DateTimeOffset markedAt, CancellationToken cancellationToken) =>
        Task.FromResult<uint?>(null);
}
