using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyDeposit;

public sealed record CreateSurveyDepositPolicyCommand(
    decimal AmountVnd,
    int ExpectedVersion)
    : IRequest<Result<SurveyDepositPolicyResponse>>;
