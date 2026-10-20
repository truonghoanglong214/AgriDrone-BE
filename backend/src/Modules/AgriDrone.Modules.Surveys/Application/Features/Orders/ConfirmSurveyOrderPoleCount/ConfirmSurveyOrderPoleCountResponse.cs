using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.ConfirmSurveyOrderPoleCount;

public sealed record ConfirmSurveyOrderPoleCountResponse(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid TenantId,
    Guid FarmId,
    Guid SurveyServiceId,
    Guid FarmBoundaryVersionId,
    Guid FarmBaseMapVersionId,
    SurveyPoleCountSource PoleCountSource,
    string PoleCountEvidenceReference,
    int ConfirmedSurveyPoleCount,
    Guid SurveyServicePriceId,
    decimal PricePerPole,
    string Currency,
    decimal FinalPrice,
    Guid SystemManagerProfileId,
    SurveyOrderStatus OrderStatus,
    uint OrderVersion,
    DateTimeOffset ConfirmedAt,
    decimal? RemainingAmount = null);
