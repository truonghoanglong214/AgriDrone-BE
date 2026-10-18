using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record ConfirmSurveyOrderPoleCountRequest(
    [param: Required] Guid? ExpectedFarmBaseMapVersionId,
    [param: Required]
    [param: StringLength(1000, MinimumLength = 1)]
    string EvidenceReference,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
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
