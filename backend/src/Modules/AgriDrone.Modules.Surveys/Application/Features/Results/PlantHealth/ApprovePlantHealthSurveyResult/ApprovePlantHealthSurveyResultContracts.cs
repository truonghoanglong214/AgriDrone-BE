using System.Text.Json.Serialization;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.ApprovePlantHealthSurveyResult;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApprovePlantHealthSurveyResultRequest(
    Guid OperationId, Guid BatchId, uint ExpectedResultVersion, long ExpectedReviewRevision, string Reason);

public sealed record ApprovePlantHealthSurveyResultResponse(
    Guid OperationId, Guid ResultId, Guid BatchId, uint ResultVersion, long ApprovedReviewRevision,
    string Status, string ResultNature, string TeamLabelLimitation, string Conclusion,
    int AcceptedFindingCount, Guid ReviewedBy, DateTimeOffset ReviewedAt,
    string ApprovalFingerprint, bool IsReplay = false, string CurrentStatus = "APPROVED");
