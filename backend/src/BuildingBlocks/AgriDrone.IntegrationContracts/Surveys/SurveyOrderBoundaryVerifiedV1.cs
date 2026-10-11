namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyOrderBoundaryVerifiedV1(
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    Guid FarmBoundaryId,
    int FarmBoundaryVersionNumber,
    Guid SystemManagerProfileId,
    bool RequiresBaselineMapping,
    Guid? CurrentFarmBaseMapVersionId,
    int? ConfirmedSurveyPoleCount,
    string OrderStatus,
    DateTimeOffset VerifiedAt);
