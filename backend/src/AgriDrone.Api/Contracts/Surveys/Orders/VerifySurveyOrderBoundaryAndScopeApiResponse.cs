namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record VerifySurveyOrderBoundaryAndScopeApiResponse(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid TenantId,
    Guid FarmId,
    Guid FarmBoundaryId,
    int FarmBoundaryVersionNumber,
    Guid SystemManagerProfileId,
    bool RequiresBaselineMapping,
    Guid? CurrentFarmBaseMapVersionId,
    int? ConfirmedSurveyPoleCount,
    string OrderStatus,
    uint OrderVersion,
    DateTimeOffset VerifiedAt);
