using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyDeposit;

public sealed record GetSurveyDepositPoliciesQuery()
    : IRequest<Result<IReadOnlyList<SurveyDepositPolicyResponse>>>;
