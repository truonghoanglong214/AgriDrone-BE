using AgriDrone.Modules.Surveys.Application.Features.Payments.GetSurveyPaymentHistory;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.GetSystemAdminSurveyPaymentHistory;

public sealed record GetSystemAdminSurveyPaymentHistoryQuery(Guid SurveyOrderId)
    : IRequest<Result<SurveyPaymentHistoryResponse>>;
