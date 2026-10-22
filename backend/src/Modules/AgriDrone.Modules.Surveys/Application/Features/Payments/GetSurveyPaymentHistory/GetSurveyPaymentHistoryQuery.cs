using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.GetSurveyPaymentHistory;

public sealed record GetSurveyPaymentHistoryQuery(Guid SurveyOrderId)
    : IRequest<Result<SurveyPaymentHistoryResponse>>;
