using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyComboDiscount;

internal sealed class GetSurveyComboDiscountPoliciesQueryHandler(
    ISurveyComboDiscountPolicyRepository repository)
    : IRequestHandler<GetSurveyComboDiscountPoliciesQuery,
        Result<IReadOnlyList<SurveyComboDiscountPolicyResponse>>>
{
    public async Task<Result<IReadOnlyList<SurveyComboDiscountPolicyResponse>>> Handle(
        GetSurveyComboDiscountPoliciesQuery request,
        CancellationToken cancellationToken)
    {
        var history = await repository.GetHistoryAsync(cancellationToken);
        return Result.Success<IReadOnlyList<SurveyComboDiscountPolicyResponse>>(
            history.Select(policy => new SurveyComboDiscountPolicyResponse(
                policy.Id,
                policy.VersionNumber,
                policy.DiscountPercentage,
                policy.EffectiveFrom,
                policy.EffectiveTo,
                policy.CreatedBy)).ToArray());
    }
}

