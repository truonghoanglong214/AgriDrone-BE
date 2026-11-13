using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyDeposit;

internal sealed class GetSurveyDepositPoliciesQueryHandler(
    ISurveyDepositPolicyRepository repository)
    : IRequestHandler<GetSurveyDepositPoliciesQuery,
        Result<IReadOnlyList<SurveyDepositPolicyResponse>>>
{
    public async Task<Result<IReadOnlyList<SurveyDepositPolicyResponse>>> Handle(
        GetSurveyDepositPoliciesQuery request,
        CancellationToken cancellationToken)
    {
        var history = await repository.GetHistoryAsync(cancellationToken);
        return Result.Success<IReadOnlyList<SurveyDepositPolicyResponse>>(
            history.Select(policy => new SurveyDepositPolicyResponse(
                policy.Id,
                policy.VersionNumber,
                policy.AmountVnd,
                policy.EffectiveFrom,
                policy.EffectiveTo,
                policy.CreatedBy)).ToArray());
    }
}
