using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.VerifySurveyOrderBoundaryAndScope;

public sealed record VerifySurveyOrderBoundaryAndScopeResponse(
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
    SurveyOrderStatus OrderStatus,
    uint OrderVersion,
    DateTimeOffset VerifiedAt);
