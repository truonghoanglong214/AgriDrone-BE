namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyRequestApprovedV1(
    Guid ApprovalReviewId,
    Guid SurveyRequestId,
    string RequestKind,
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    Guid SurveyServiceId,
    Guid SystemManagerProfileId,
    Guid? PrimaryManagerAssignmentId,
    bool RequiresBaselineMapping,
    Guid? PreviousCompatibleOrderId,
    DateTimeOffset ApprovedAt);
