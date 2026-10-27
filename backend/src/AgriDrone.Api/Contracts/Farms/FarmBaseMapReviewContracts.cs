namespace AgriDrone.Api.Contracts.Farms;

public sealed record ReviewCandidateApiRequest(
    string Decision,
    double? CorrectedLongitude,
    double? CorrectedLatitude,
    int? CorrectedRowIndex,
    int? CorrectedColumnIndex,
    Guid? CorrectedMatchedPlantId,
    string? Reason);

public sealed record CandidateBatchReviewItemApiRequest(
    Guid CandidateId,
    string Decision,
    double? CorrectedLongitude,
    double? CorrectedLatitude,
    int? CorrectedRowIndex,
    int? CorrectedColumnIndex,
    Guid? CorrectedMatchedPlantId,
    string? Reason);

public sealed record ReviewCandidatesBatchApiRequest(
    IReadOnlyList<CandidateBatchReviewItemApiRequest> Reviews,
    string? BatchReason);
