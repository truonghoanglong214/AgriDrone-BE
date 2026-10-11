using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record ConfirmSurveyOrderPoleCountRequest(
    [property: Required] Guid? ExpectedFarmBaseMapVersionId,
    [property: Required]
    [property: StringLength(1000, MinimumLength = 1)]
    string EvidenceReference,
    [property: Required]
    [property: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion);

public sealed record ConfirmSurveyOrderPoleCountApiResponse(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid TenantId,
    Guid FarmId,
    Guid SurveyServiceId,
    Guid FarmBoundaryVersionId,
    Guid FarmBaseMapVersionId,
    string PoleCountSource,
    string PoleCountEvidenceReference,
    int ConfirmedSurveyPoleCount,
    Guid SurveyServicePriceId,
    decimal PricePerPole,
    string Currency,
    decimal FinalPrice,
    Guid SystemManagerProfileId,
    string OrderStatus,
    uint OrderVersion,
    DateTimeOffset ConfirmedAt);
