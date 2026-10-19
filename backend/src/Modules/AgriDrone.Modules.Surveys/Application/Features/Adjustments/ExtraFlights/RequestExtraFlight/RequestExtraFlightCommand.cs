using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.RequestExtraFlight;

public sealed record RequestExtraFlightCommand(
    Guid SurveyOrderId,
    Guid FarmId,
    Guid? SourceMissionId,
    ExtraFlightReason Reason,
    string EvidenceReference,
    decimal CostImpact,
    string Currency)
    : IRequest<Result<ExtraFlightRequestResponse>>;

public sealed record ExtraFlightRequestResponse(
    Guid Id,
    Guid SurveyOrderId,
    Guid FarmId,
    Guid? SourceMissionId,
    Guid? ExtraMissionId,
    ExtraFlightReason Reason,
    string EvidenceReference,
    decimal CostImpact,
    string Currency,
    ExtraFlightRequestStatus Status,
    Guid RequestedBy,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    uint Version,
    DateTimeOffset CreatedAt);
