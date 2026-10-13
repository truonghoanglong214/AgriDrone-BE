using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequest;

public sealed record SubmitNewCustomerSurveyRequestCommand(
    RequestIdempotency Idempotency,
    Guid SurveyServiceId,
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
    : IRequest<Result<SubmitNewCustomerSurveyRequestResponse>>;
