namespace AgriDrone.Modules.Surveys.Application.Abstractions.Orders;

public sealed record SurveyBoundaryApprovalSnapshot(
    Guid FarmBoundaryId,
    int VersionNumber);
