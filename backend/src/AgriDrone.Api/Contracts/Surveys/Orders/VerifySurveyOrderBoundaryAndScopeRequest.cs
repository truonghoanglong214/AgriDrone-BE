using System.ComponentModel.DataAnnotations;
using AgriDrone.Api.Contracts.Farms;

namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record VerifySurveyOrderBoundaryAndScopeRequest(
    [property: Required] GeoJsonPolygonRequest Boundary,
    [property: Required]
    [property: StringLength(2000, MinimumLength = 1)]
    string Reason,
    [property: StringLength(1000)] string? EvidenceReference,
    [property: Required]
    [property: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion,
    [property: Range(1, int.MaxValue)]
    int? ExpectedCurrentBoundaryVersionNumber);

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
