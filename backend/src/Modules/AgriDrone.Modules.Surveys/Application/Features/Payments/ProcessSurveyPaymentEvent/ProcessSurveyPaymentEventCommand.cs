using System.Text.Json;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ProcessSurveyPaymentEvent;

public sealed record ProcessSurveyPaymentEventCommand(
    string Provider,
    string ProviderReference,
    string ProviderEventId,
    string EventType,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    JsonDocument Payload,
    string? ProviderTransactionId = null)
    : IRequest<Result<ProcessSurveyPaymentEventResponse>>;
