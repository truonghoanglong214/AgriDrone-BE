using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequestV3;

public sealed record SubmitNewCustomerSurveyRequestV3Command(
    RequestIdempotency Idempotency,
    Guid SurveyServiceId,
    string TenantCode,
    string TenantName,
    string ApplicantName,
    string ApplicantEmail,
    string ApplicantPhone,
    string FarmName,
    string FarmAddress,
    decimal ApproximateAreaHa,
    double Longitude,
    double Latitude,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes)
    : IRequest<Result<SubmitNewCustomerSurveyRequestV3Response>>;
