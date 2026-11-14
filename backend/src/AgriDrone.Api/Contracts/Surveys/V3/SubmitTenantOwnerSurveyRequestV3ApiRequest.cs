using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.V3;

public sealed record SubmitTenantOwnerSurveyRequestV3ApiRequest(
    [param: Required]
    Guid SurveyServiceId,
    Guid? FarmId,
    [param: StringLength(200)]
    string? FarmName,
    [param: StringLength(500)]
    string? FarmAddress,
    [param: Range(typeof(decimal), "0.01", "10000.00")]
    decimal? ApproximateAreaHa,
    [param: Range(-180.0, 180.0)]
    double? Longitude,
    [param: Range(-90.0, 90.0)]
    double? Latitude,
    [param: Range(1, 1_000_000)]
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    [param: StringLength(2_000)]
    string? Notes);
