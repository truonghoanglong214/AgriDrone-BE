using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.V3;

public sealed record SubmitNewCustomerSurveyRequestV3ApiRequest(
    [param: Required]
    Guid SurveyServiceId,
    [param: Required, StringLength(100, MinimumLength = 1)]
    string TenantCode,
    [param: Required, StringLength(200, MinimumLength = 1)]
    string TenantName,
    [param: Required, StringLength(200, MinimumLength = 1)]
    string ApplicantName,
    [param: Required, EmailAddress, StringLength(320, MinimumLength = 3)]
    string ApplicantEmail,
    [param: Required, Phone, StringLength(50, MinimumLength = 1)]
    string ApplicantPhone,
    [param: Required, StringLength(200, MinimumLength = 1)]
    string FarmName,
    [param: Required, StringLength(500, MinimumLength = 1)]
    string FarmAddress,
    [param: Range(typeof(decimal), "0.01", "10000.00")]
    decimal ApproximateAreaHa,
    [param: Range(-180.0, 180.0)]
    double Longitude,
    [param: Range(-90.0, 90.0)]
    double Latitude,
    [param: Range(1, 1_000_000)]
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    [param: StringLength(2_000)]
    string? Notes);
